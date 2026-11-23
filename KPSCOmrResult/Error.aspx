<%@ Page Language="C#" EnableSessionState="false" EnableViewState="false" %>
<%-- Shown to visitors when a request fails (customErrors in Web.config).
     Details are in App_Data\logs, never on this page. --%>
<script runat="server">
    // Set by Application_Error when the fault was logged; shown so a user's
    // report can be matched to the log entry. Nothing else about the error is shown.
    protected string Reference { get; private set; }

    protected void Page_Load(object sender, EventArgs e)
    {
        Response.StatusCode = 500;
        Response.TrySkipIisCustomErrors = true;
        Response.Cache.SetCacheability(HttpCacheability.NoCache);
        Response.Cache.SetNoStore();

        Reference = Context.Items[KPSCOmrResult.Global.ErrorReferenceKey] as string;
    }
</script>
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>Something went wrong - OMR Result Portal</title>
    <style>
        body { font-family: Arial, Helvetica, sans-serif; background: #f4f6f9; margin: 0; }
        .box { max-width: 520px; margin: 90px auto; background: #fff; padding: 32px;
               border-radius: 8px; box-shadow: 0 2px 8px rgba(0,0,0,.08); text-align: center; }
        h1 { font-size: 22px; margin: 0 0 12px; color: #1b2a49; }
        p { color: #444; line-height: 1.5; }
        a { color: #0d6efd; }
        .ref { font-size: 14px; color: #666; }
    </style>
    <script src="<%: ResolveUrl("~/Scripts/no-context-menu.js") %>"></script>
</head>
<body>
    <div class="box">
        <h1>Something went wrong</h1>
        <p>
            The page could not be shown. Nothing you entered on the previous
            page has been lost if it was already saved.
        </p>
        <p>
            Please go back and try again. If you were making a payment, do not
            pay again: log in and open File Objection to check its status.
        </p>
        <% if (!string.IsNullOrEmpty(Reference)) { %>
        <p class="ref">If you contact KPSC about this, quote reference <strong><%: Reference %></strong>.</p>
        <% } %>
        <p><a href="<%: ResolveUrl("~/Login") %>">Return to login</a></p>
    </div>
</body>
</html>
