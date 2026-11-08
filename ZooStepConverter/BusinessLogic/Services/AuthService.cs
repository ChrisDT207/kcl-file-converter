using System;
using System.Diagnostics;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Threading;
using ZooStepConverter.BusinessLogic.Common;
using ZooStepConverter.DataAccess;

namespace ZooStepConverter.BusinessLogic.Services
{
    /// <summary>
    /// Business Logic Layer: Handles Zoo authentication, OAuth2 device flow, and account preflight validation.
    /// </summary>
    public class AuthService
    {
        private readonly ZooProcessRunner _processRunner;

        public AuthService(ZooProcessRunner processRunner)
        {
            _processRunner = processRunner ?? new ZooProcessRunner();
        }

        public void Preflight(string apiToken, Action<string> log, Action<string, Color> setStatus)
        {
            log("Checking Zoo CLI...");
            string outp;
            int code = _processRunner.RunZoo("version", null, apiToken, out outp);
            if (code < 0)
            {
                log("[ERROR] The Zoo CLI is not installed (or not in PATH).");
                log("Install it from https://zoo.dev/docs/developer-tools/cli then restart this app.");
                setStatus("Zoo CLI missing", Color.Firebrick);
                return;
            }

            string verLine = TextHelpers.FirstLine(outp);
            log("  Zoo CLI found: " + verLine);

            // Check if CLI version is < 0.2.204 (which lacks stable KCL 3.0 support)
            var verMatch = Regex.Match(verLine, @"\b(\d+)\.(\d+)\.(\d+)\b");
            if (verMatch.Success)
            {
                int major = int.Parse(verMatch.Groups[1].Value);
                int minor = int.Parse(verMatch.Groups[2].Value);
                int patch = int.Parse(verMatch.Groups[3].Value);

                if (major == 0 && (minor < 2 || (minor == 2 && patch < 204)))
                {
                    log(string.Format("  [!] Note: Installed Zoo CLI is v{0}.{1}.{2}.", major, minor, patch));
                    log("      Zoo Studio projects with KCL 3.0 require Zoo CLI >= v0.2.204.");
                    log("      Auto-fallback compatibility mode is enabled to convert these seamlessly.");
                    log("      (Click \"Update CLI\" above to upgrade to the latest CLI).");
                }
                else
                {
                    log("  [+] CLI version supports KCL 1.0, 2.0, and 3.0 natively.");
                }
            }

            code = _processRunner.RunZoo("auth status", null, apiToken, out outp);
            if (code != 0)
            {
                log("[!] Not logged in. Click \"Login with Zoo\" - a browser opens, you approve, done.");
                setStatus("Not logged in", Color.Firebrick);
                return;
            }
            log("  Logged in: " + TextHelpers.FirstLine(outp));

            // The engine's "Please send { Authorization: Bearer <token> }" websocket error
            // is usually NOT an auth problem - it is what a credit/billing-blocked account
            // sees (github.com/KittyCAD/cli/issues/1745). Only the REST API tells the truth,
            // so check the real balance here.
            code = _processRunner.RunZoo("api /user/payment/balance", null, apiToken, out outp);
            if (outp.Contains("missing_payment_method") || outp.Contains("402"))
            {
                log("[!] ACCOUNT BLOCKED: no credits remaining and no payment method on file.");
                log("    This is what causes the misleading \"Bearer <token>\" websocket error.");
                log("    Fix it at https://zoo.dev/account (add payment method or wait for the monthly reset).");
                setStatus("Out of credits", Color.Firebrick);
                return;
            }
            var monthly = Regex.Match(outp, "\"monthly_api_credits_remaining\"\\s*:\\s*([0-9.]+)");
            var stable = Regex.Match(outp, "\"stable_api_credits_remaining\"\\s*:\\s*([0-9.]+)");
            if (monthly.Success || stable.Success)
            {
                double m = monthly.Success ? double.Parse(monthly.Groups[1].Value) : 0;
                double s = stable.Success ? double.Parse(stable.Groups[1].Value) : 0;
                log(string.Format("  Credits remaining - monthly: {0}, purchased: {1}", m, s));
                if (m <= 0 && s <= 0)
                {
                    log("[!] ZERO CREDITS LEFT. Engine commands will fail with the misleading");
                    log("    \"Bearer <token>\" websocket error until credits reset or a payment");
                    log("    method is added at https://zoo.dev/account");
                    setStatus("Out of credits", Color.Firebrick);
                    return;
                }
            }
            log("Ready.");
            setStatus("Ready", Color.ForestGreen);
        }

        public void OAuthLogin(Action<string> log, Action<string, Color> setStatus, Action<string> onTokenSaved, Action onPreflightNeeded)
        {
            log("");
            log("Starting Zoo login...");
            int status;
            string resp = ZooApiClient.HttpPostForm(ZooApiClient.ApiHost + "oauth2/device/auth", "client_id=" + ZooApiClient.ClientId, out status);
            string deviceCode = TextHelpers.JsonStr(resp, "device_code");
            string userCode = TextHelpers.JsonStr(resp, "user_code");
            string uri = TextHelpers.JsonStr(resp, "verification_uri_complete");
            if (uri == null) uri = TextHelpers.JsonStr(resp, "verification_uri");
            if (deviceCode == null || uri == null)
            {
                log("[ERROR] Unexpected response from Zoo: " + TextHelpers.Truncate(resp, 300));
                return;
            }

            int interval = 5;
            var im = Regex.Match(resp, "\"interval\"\\s*:\\s*(\\d+)");
            if (im.Success) interval = Math.Max(1, int.Parse(im.Groups[1].Value));

            log("A browser window is opening. Approve the login there.");
            if (userCode != null) log("The page should show this code: " + userCode);
            try { Process.Start(uri.Replace("\\u0026", "&")); }
            catch { log("Could not open a browser automatically. Open this URL yourself: " + uri); }

            setStatus("Waiting for browser…", Color.DarkOrange);
            for (int waited = 0; waited < 900; waited += interval)
            {
                Thread.Sleep(interval * 1000);
                string body = "grant_type=urn%3Aietf%3Aparams%3Aoauth%3Agrant-type%3Adevice_code"
                    + "&device_code=" + Uri.EscapeDataString(deviceCode)
                    + "&client_id=" + ZooApiClient.ClientId;
                string tresp = ZooApiClient.HttpPostForm(ZooApiClient.ApiHost + "oauth2/device/token", body, out status);
                string token = TextHelpers.JsonStr(tresp, "access_token");
                if (token != null)
                {
                    TokenRepository.SaveToken(token);
                    if (onTokenSaved != null) onTokenSaved(token);
                    string email = null;
                    try { email = TextHelpers.JsonStr(ZooApiClient.HttpGet(ZooApiClient.ApiHost + "user", token), "email"); } catch { }
                    log("Logged in" + (email != null ? " as " + email : "") + ". Token saved for next time.");
                    if (onPreflightNeeded != null) onPreflightNeeded();
                    return;
                }
                if (tresp.Contains("slow_down")) interval += 5;
                else if (tresp.Contains("expired")) { log("[ERROR] The login code expired. Click \"Login with Zoo\" to try again."); setStatus("Login expired", Color.Firebrick); return; }
                else if (!tresp.Contains("authorization_pending") && status >= 400)
                {
                    log("[ERROR] Login failed: " + TextHelpers.Truncate(tresp, 300));
                    setStatus("Login failed", Color.Firebrick);
                    return;
                }
            }
            log("[ERROR] Login timed out. Click \"Login with Zoo\" to try again.");
            setStatus("Login timed out", Color.Firebrick);
        }
    }
}

