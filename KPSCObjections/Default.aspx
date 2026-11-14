<%@ Page Language="C#" %>
<%-- Site root: send visitors to the candidate login. --%>
<script runat="server">
    protected void Page_Load(object sender, EventArgs e)
    {
        Response.Redirect("~/Login.aspx", true);
    }
</script>
