<%@ Page Title="Candidate Login - OMR Result Portal" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Login.aspx.cs" Inherits="KPSCOmrResult.Login" %>
<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
     <link href="css/Login.css?v=6" rel="stylesheet" />
    <div class="login-page">

        <div class="login-panel">

            <div class="login-heading">
                Login
            </div>

            <div class="login-body">

                <%-- Shown only when the Security\TestMode switch is on (Web.config). --%>
                <asp:Label ID="lblTestMode" runat="server" Visible="false"
                    CssClass="alert alert-danger d-block fw-bold py-2 mb-3" />

                <%-- Examination --%>
                <div class="field-group">
                    <label class="form-label"
                        for="<%= ddlExamination.ClientID %>">
                        Select Examination<span class="required">*</span>
                    </label>

                    <asp:DropDownList ID="ddlExamination"
                        runat="server"
                        CssClass="form-select">
                    </asp:DropDownList>
                </div>

                <%-- Registration Number --%>
                <div class="field-group">
                    <label class="form-label"
                        for="<%= txtRegisterNumber.ClientID %>">
                        Registration No.<span class="required">*</span>
                    </label>

                    <asp:TextBox ID="txtRegisterNumber"
                        runat="server"
                        MaxLength="10"
                        autocomplete="off"
                        CssClass="form-control" />
                </div>

                <%-- Date of Birth --%>
                <div class="field-group">
                    <label class="form-label"
                        for="<%= txtDob.ClientID %>">
                        Date of Birth<span class="required">*</span>
                    </label>

                    <asp:TextBox ID="txtDob"
                        runat="server"
                        MaxLength="10"
                        autocomplete="bday"
                        inputmode="numeric"
                        data-dob-mask="true"
                        CssClass="form-control"
                        placeholder="DD-MM-YYYY" />
                </div>

                <%-- Captcha: a new one for every login attempt --%>
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

                <%-- Status Message --%>
                <asp:Label ID="lblMessage"
                    runat="server"
                    CssClass="text-danger mt-3 d-block login-message">
                </asp:Label>

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

    <%-- Adds the dashes of DD-MM-YYYY while the date of birth is typed. --%>
    <script src="<%: ResolveUrl("~/Scripts/dob-mask.js") %>"></script>
</asp:Content>
