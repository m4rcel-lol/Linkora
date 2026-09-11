using System;
using System.Text;

namespace WLMServer.Network
{
    /// <summary>
    /// The sign up page the server hosts. It is written out by hand rather than loaded from disk so
    /// that a server is one binary and a config file, with nothing to lose when it is copied about.
    /// </summary>
    static class SignUpPage
    {
        /// <summary>The form, optionally redisplayed with a problem to fix.</summary>
        public static string Form(string problem, string username)
        {
            StringBuilder page = new StringBuilder();

            Open(page, "Create a " + Config.Properties.SERVER_NAME + " ID");

            page.Append("<h1>").Append(Escape(Config.Properties.SERVER_NAME)).Append("</h1>");
            page.Append("<p class=\"lead\">Create an ID to sign in with.</p>");

            if (!string.IsNullOrEmpty(problem))
            {
                page.Append("<p class=\"problem\">").Append(Escape(problem)).Append("</p>");
            }

            page.Append("<form method=\"post\" action=\"/signup\">");
            page.Append("<label for=\"username\">Sign in name</label>");
            page.Append("<input id=\"username\" name=\"username\" autocomplete=\"username\" autofocus")
                .Append(" maxlength=\"29\" value=\"").Append(Escape(username ?? "")).Append("\">");
            page.Append("<p class=\"hint\">Letters, numbers, dots, dashes and underscores.</p>");

            page.Append("<label for=\"password\">Password</label>");
            page.Append("<input id=\"password\" name=\"password\" type=\"password\" autocomplete=\"new-password\">");
            page.Append("<p class=\"hint\">At least six characters.</p>");

            page.Append("<label for=\"confirm\">Password again</label>");
            page.Append("<input id=\"confirm\" name=\"confirm\" type=\"password\" autocomplete=\"new-password\">");

            page.Append("<button type=\"submit\">Create my ID</button>");
            page.Append("</form>");

            Close(page);

            return page.ToString();
        }

        /// <summary>Shown once the account exists.</summary>
        public static string Done(string username)
        {
            StringBuilder page = new StringBuilder();

            Open(page, "Your " + Config.Properties.SERVER_NAME + " ID is ready");

            page.Append("<h1>").Append(Escape(Config.Properties.SERVER_NAME)).Append("</h1>");
            page.Append("<p class=\"done\">Your ID is ready.</p>");
            page.Append("<p class=\"lead\">Sign in as <strong>").Append(Escape(username))
                .Append("</strong> in Linkora, using the same server address you used to get here.</p>");
            page.Append("<p><a href=\"/signup\">Create another ID</a></p>");

            Close(page);

            return page.ToString();
        }

        private static void Open(StringBuilder page, string title)
        {
            page.Append("<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\">");
            page.Append("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
            page.Append("<title>").Append(Escape(title)).Append("</title>");
            page.Append("<style>").Append(Style).Append("</style>");
            page.Append("</head><body><main>");
        }

        private static void Close(StringBuilder page)
        {
            page.Append("</main></body></html>");
        }

        private const string Style =
            ":root{color-scheme:light dark}" +
            "body{margin:0;min-height:100vh;display:flex;align-items:center;justify-content:center;" +
            "background:linear-gradient(#eaf5fb,#c9e7f3);font:15px/1.5 -apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;color:#22303c}" +
            "main{width:100%;max-width:23rem;margin:2rem;padding:1.75rem;box-sizing:border-box;" +
            "background:#fff;border:1px solid #b9d7e6;border-radius:6px;box-shadow:0 1px 3px rgba(0,0,0,.08)}" +
            "h1{margin:0;font-size:1.5rem;font-weight:600;color:#355a88}" +
            ".lead{margin:.25rem 0 1.25rem;color:#4a5b6b}" +
            "label{display:block;margin-top:1rem;font-weight:600;font-size:.875rem}" +
            "input{width:100%;box-sizing:border-box;margin-top:.35rem;padding:.5rem;font-size:1rem;" +
            "border:1px solid #b9d7e6;border-radius:3px;background:#fff;color:inherit}" +
            "input:focus{outline:2px solid #355a88;outline-offset:-1px}" +
            ".hint{margin:.3rem 0 0;font-size:.8125rem;color:#6b7a89}" +
            "button{margin-top:1.5rem;width:100%;padding:.6rem;font-size:1rem;font-weight:600;" +
            "border:1px solid #355a88;border-radius:3px;background:#355a88;color:#fff;cursor:pointer}" +
            "button:hover{background:#2b4a72}" +
            ".problem{margin:0 0 1rem;padding:.6rem .75rem;border-radius:3px;" +
            "background:#fdeaea;border:1px solid #e7b4b4;color:#8a2b2b}" +
            ".done{margin:.25rem 0 1rem;padding:.6rem .75rem;border-radius:3px;" +
            "background:#eaf7ec;border:1px solid #a9d5b2;color:#2b6b3a;font-weight:600}" +
            "a{color:#355a88}" +
            "@media (prefers-color-scheme:dark){" +
            "body{background:linear-gradient(#1b1e23,#121417);color:#e4e6ea}" +
            "main{background:#26282c;border-color:#3a3d42}" +
            "h1,a{color:#8ab4e8}" +
            ".lead{color:#a8b0ba}.hint{color:#98a0aa}" +
            "input{background:#1f2227;border-color:#3a3d42}" +
            "button{background:#3d6ea8;border-color:#3d6ea8}button:hover{background:#4a7fbd}" +
            ".problem{background:#3a2323;border-color:#6b3a3a;color:#f0b4b4}" +
            ".done{background:#1f3325;border-color:#3a6b45;color:#a9d5b2}}";

        /// <summary>
        /// Everything put into the page goes through here. A sign in name is rejected long before
        /// it reaches the page, but the name is echoed back on failure, so it is escaped anyway.
        /// </summary>
        private static string Escape(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return "";
            }

            return text
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&#39;");
        }
    }
}
