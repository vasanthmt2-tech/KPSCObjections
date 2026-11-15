<%@ Page Language="C#" EnableSessionState="false" EnableViewState="false" %>
<%-- Shown for addresses that do not exist (customErrors / httpErrors in Web.config). --%>
<script runat="server">
    protected void Page_Load(object sender, EventArgs e)
    {
        Response.StatusCode = 404;
        Response.TrySkipIisCustomErrors = true;
    }
</script>
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>Page not found - Answer Key Objection Portal</title>
    <style>
        body { font-family: Arial, Helvetica, sans-serif; background: #f4f6f9; margin: 0; }
        .box { max-width: 520px; margin: 90px auto; background: #fff; padding: 32px;
               border-radius: 8px; box-shadow: 0 2px 8px rgba(0,0,0,.08); text-align: center; }
        h1 { font-size: 22px; margin: 0 0 12px; color: #1b2a49; }
        p { color: #444; line-height: 1.5; }
        a { color: #0d6efd; }
    </style>
    <script src="<%: ResolveUrl("~/Scripts/no-context-menu.js") %>"></script>
</head>
<body>
    <div class="box">
        <h1>Page not found</h1>
        <p>The page you asked for does not exist. Please check the address.</p>
        <p><a href="<%: ResolveUrl("~/Login") %>">Go to login</a></p>
    </div>
</body>
</html>
