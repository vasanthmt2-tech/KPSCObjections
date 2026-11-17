<%@ Page Title="Audit Trail - Answer Key Objection Portal" Language="C#" MasterPageFile="~/Admin.Master" AutoEventWireup="true" CodeBehind="AuditTrail.aspx.cs" Inherits="KPSCObjections.AuditTrail" %>
<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">

    <div class="container mt-4 mb-5">

        <h2 class="h3">Audit Trail</h2>
        <p class="text-muted">
            Actions of admins: sign-ins (and failed ones), uploads, deletes, downloads,
            objection window changes and payment re-checks. Entries cannot be changed or deleted.
        </p>

        <asp:Panel ID="pnlFilter" runat="server" DefaultButton="btnSearch" CssClass="row g-2 align-items-end mb-3">
            <div class="col-6 col-md-2">
                <label class="form-label mb-0" for="<%= txtFrom.ClientID %>">From</label>
                <asp:TextBox ID="txtFrom" runat="server" TextMode="Date" CssClass="form-control" />
            </div>
            <div class="col-6 col-md-2">
                <label class="form-label mb-0" for="<%= txtTo.ClientID %>">To</label>
                <asp:TextBox ID="txtTo" runat="server" TextMode="Date" CssClass="form-control" />
            </div>
            <div class="col-6 col-md-2">
                <label class="form-label mb-0" for="<%= ddlAdmin.ClientID %>">Admin</label>
                <asp:DropDownList ID="ddlAdmin" runat="server" CssClass="form-select" />
            </div>
            <div class="col-6 col-md-3">
                <label class="form-label mb-0" for="<%= ddlAction.ClientID %>">Action</label>
                <asp:DropDownList ID="ddlAction" runat="server" CssClass="form-select" />
            </div>
            <div class="col-12 col-md-3 d-flex gap-2">
                <asp:Button ID="btnSearch" runat="server" Text="Show" CssClass="btn btn-primary"
                    CausesValidation="false" OnClick="btnSearch_Click" />
                <asp:Button ID="btnReset" runat="server" Text="Last 7 days" CssClass="btn btn-outline-secondary"
                    CausesValidation="false" OnClick="btnReset_Click" />
                <asp:Button ID="btnExcel" runat="server" Text="Download Excel" CssClass="btn btn-outline-success"
                    CausesValidation="false" OnClick="btnExcel_Click" />
            </div>
        </asp:Panel>

        <div class="mb-3">
            <asp:Label ID="lblSummary" runat="server" CssClass="fw-bold" />
        </div>

        <%-- Scrolls inside its own box on narrow screens. --%>
        <div class="table-responsive">
        <asp:GridView ID="gvAudit"
            runat="server"
            AutoGenerateColumns="False"
            CssClass="table table-bordered table-striped table-hover"
            GridLines="None"
            AllowPaging="True"
            PageSize="50"
            EmptyDataText="No audit entries for these filters."
            OnPageIndexChanging="gvAudit_PageIndexChanging">
            <Columns>
                <asp:BoundField DataField="AuditTime" HeaderText="Time" DataFormatString="{0:dd-MMM-yyyy hh:mm:ss tt}"
                    ItemStyle-CssClass="text-nowrap" />
                <asp:BoundField DataField="AdminUser" HeaderText="Admin" />
                <asp:BoundField DataField="Action" HeaderText="Action" ItemStyle-CssClass="text-nowrap" />
                <asp:BoundField DataField="Details" HeaderText="Details" ItemStyle-CssClass="text-break" ItemStyle-Width="45%" />
                <asp:BoundField DataField="ClientIp" HeaderText="IP address" />
            </Columns>
            <HeaderStyle CssClass="table-dark" />
            <PagerStyle CssClass="grid-pager" />
        </asp:GridView>
        </div>

    </div>

</asp:Content>
