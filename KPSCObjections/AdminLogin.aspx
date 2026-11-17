<%@ Page Title="" Language="C#" MasterPageFile="~/Admin.Master" AutoEventWireup="true" CodeBehind="AdminLogin.aspx.cs" Inherits="KPSCObjections.AdminLogin" %>
<asp:Content ID="Content1" ContentPlaceHolderID="HeadContent" runat="server">
    
</asp:Content>
<asp:Content ID="Content2" ContentPlaceHolderID="MainContent" runat="server">
    <link href="css/Login.css" rel="stylesheet" />
    <div class="login-page">

        <div class="login-panel">

            <div class="login-heading">
                Login 
            </div>

            <div class="login-body">

                <%-- Shown only when Security\TestMode switches are on (Web.config). --%>
                <asp:Label ID="lblTestMode" runat="server" Visible="false"
                    CssClass="alert alert-danger d-block fw-bold py-2 mb-3" />

                <%-- Application / Register Number --%>
                <div class="field-group">
                    <label class="form-label"
                        for="<%= txtEmpId.ClientID %>">
                        User Name.
                    </label>

                    <asp:TextBox ID="txtEmpId"
                        runat="server"
                        autocomplete="off"
                        CssClass="form-control" />
                </div>

                <%-- Candidate Name --%>
                <%--<div class="field-group">
                    <label class="form-label"
                        for="<%= txtCandidateName.ClientID %>">
                        Candidate Name
                    </label>

                    <asp:TextBox ID="txtCandidateName"
                        runat="server"
                        CssClass="form-control" />
                </div>--%>

                <%-- Date of Birth --%>
                <div class="field-group">
                    <label class="form-label"
                        for="<%= txtEmpPwd.ClientID %>">
                        Password
                    </label>

                    <asp:TextBox ID="txtEmpPwd" runat="server" TextMode="Password" CssClass="form-control" autocomplete="off" />

                </div>

                <%-- Examination --%>
             <%--   <div class="field-group">
                    <label class="form-label"
                        for="<%= ddlExamination.ClientID %>">
                        Select Examination<span class="required">*</span>
                    </label>

                    <asp:DropDownList ID="ddlExamination"
                        runat="server"
                        CssClass="form-select">                     
                    </asp:DropDownList>
                </div>--%>

                <%-- Captcha: checked before the password --%>
                <asp:PlaceHolder ID="phCaptcha" runat="server">
                <div class="captcha-section">

                    <label class="form-label"
                        for="<%= txtCaptcha.ClientID %>">
                        Enter the captcha<span class="required">*</span>
                    </label>

                    <asp:TextBox ID="txtCaptcha"
                        runat="server"
                        MaxLength="6"
                        autocomplete="off"
                        CssClass="form-control"
                        placeholder="Enter captcha as shown below" />

                    <div class="captcha-display">
                        <asp:Image ID="imgCaptcha"
                            runat="server"
                            AlternateText="Captcha" />

                        <asp:LinkButton ID="btnRefreshCaptcha"
                            runat="server"
                            CssClass="captcha-refresh"
                            CausesValidation="false"
                            OnClick="btnRefreshCaptcha_Click1"
                            ToolTip="Refresh Captcha">
                            <i class="bi bi-arrow-repeat"></i>
                        </asp:LinkButton>
                    </div>
                </div>
                </asp:PlaceHolder>

                <%-- Login Button --%>
                <div class="login-button-section">
                    <asp:Button ID="btnLogin"
                        runat="server"
                        Text="Login"
                        CssClass="btn btn-primary login-button"
                        OnClick="btnLogin_Click" />
                </div>
            </div>
        </div>

    </div>
</asp:Content>
