<%@ Page Title="" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Login.aspx.cs" Inherits="KPSCObjections.Login" %>
<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
     <link href="css/Login.css" rel="stylesheet" />
    <div class="login-page">

        <div class="login-panel">

            <div class="login-heading">
                Login
            </div>

            <div class="login-body">

                <!-- Step 1: identify the candidate and request an OTP -->

                <!-- Registration Number -->
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

                <!-- Examination -->
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

                <!-- Captcha: required to send an OTP -->
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
                            ImageUrl="~/Captcha.aspx"
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

                <!-- Get OTP and Resend OTP Buttons -->
                <div class="otp-button-row">

                    <asp:Button ID="btnGetOTP"
                        runat="server"
                        Text="Get OTP"
                        CssClass="btn btn-success"
                        OnClick="btnGetOTP_Click" />

                    <asp:Button ID="btnResendOTP"
                        runat="server"
                        Text="Resend OTP"
                        CssClass="btn btn-outline-primary"
                        CausesValidation="false"
                        OnClick="btnResendOTP_Click" />

                </div>

                <!-- Status Message -->
                <asp:Label ID="lblMessage"
                    runat="server"
                    CssClass="text-danger mt-3 d-block">
                </asp:Label>

                <!-- Step 2: enter the OTP and log in -->
                <asp:Panel ID="pnlOTP" runat="server" Visible="true">

                    <div class="otp-row">

                        <div class="otp-input">
                            <label class="form-label"
                                for="<%= txtOTP.ClientID %>">
                                Enter OTP<span class="required">*</span>
                            </label>

                            <asp:TextBox ID="txtOTP"
                                runat="server"
                                CssClass="form-control"
                                MaxLength="6"
                                autocomplete="one-time-code"
                                inputmode="numeric"
                                placeholder="Enter 6-digit OTP" />
                        </div>

                    </div>

                </asp:Panel>

                <!-- Login Button -->
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
