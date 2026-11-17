<%@ Page Title="Candidate Objections - Answer Key Objection Portal" Language="C#" MasterPageFile="~/Admin.Master" AutoEventWireup="true" CodeBehind="CandidateObjections.aspx.cs" Inherits="KPSCObjections.CandidateObjections" %>
<asp:Content ID="Content1"
    ContentPlaceHolderID="MainContent"
    runat="server">

    <div class="container mt-4">

        <h2 class="h3">Candidate Objections</h2>
        <p class="text-muted">Submitted objections only: those whose fee payment succeeded.</p>

        <%-- Search: Enter in the box runs the search --%>
        <asp:Panel ID="pnlSearch" runat="server" DefaultButton="btnSearch"
            CssClass="row g-2 align-items-center mb-3">
            <div class="col-auto">
                <label class="form-label mb-0" for="<%= txtRegNo.ClientID %>">Registration Number</label>
            </div>
            <div class="col-auto">
                <asp:TextBox ID="txtRegNo"
                    runat="server"
                    CssClass="form-control"
                    MaxLength="10"
                    autocomplete="off"
                    placeholder="Enter registration number" />
            </div>
            <div class="col-auto">
                <asp:Button ID="btnSearch"
                    runat="server"
                    Text="Search"
                    CssClass="btn btn-primary"
                    CausesValidation="false"
                    OnClick="btnSearch_Click" />
            </div>
            <div class="col-auto">
                <asp:Button ID="btnShowAll"
                    runat="server"
                    Text="Show All"
                    CssClass="btn btn-outline-secondary"
                    CausesValidation="false"
                    OnClick="btnShowAll_Click" />
            </div>
        </asp:Panel>

        <%-- Number of objections for the search (or for everyone) --%>
        <div class="mb-3">
            <asp:Label ID="lblSummary" runat="server" CssClass="fw-bold" />
        </div>

        <%-- Scrolls inside its own box on narrow screens. --%>
        <div class="table-responsive">
        <asp:GridView ID="gvObjections"
            runat="server"
            AutoGenerateColumns="False"
            CssClass="table table-bordered table-striped table-hover"
            GridLines="None"
            AllowPaging="True"
            PageSize="50"
            EmptyDataText="No submitted objections found."
            OnPageIndexChanging="gvObjections_PageIndexChanging">

            <Columns>

                <asp:TemplateField HeaderText="Sl. No.">
                    <ItemTemplate>
                        <%# Container.DataItemIndex + 1 %>
                    </ItemTemplate>
                </asp:TemplateField>

                <asp:BoundField DataField="Regno" HeaderText="Reg. No." />
                <asp:BoundField DataField="PaperCode" HeaderText="Paper Code" />
                <asp:BoundField DataField="VersionCode" HeaderText="Version Code" />
                <asp:BoundField DataField="QuestionNo" HeaderText="Question No." />
                <asp:BoundField DataField="KeyAnswer" HeaderText="Key Answer" />
                <asp:BoundField DataField="ObjectionAnswer" HeaderText="Objection Answer" />
                <asp:BoundField DataField="Category" HeaderText="Category" />
                <asp:BoundField DataField="Remarks" HeaderText="Remarks"
                    ItemStyle-CssClass="text-break" ItemStyle-Width="35%" />

            </Columns>

            <HeaderStyle CssClass="table-dark" />
            <PagerStyle CssClass="grid-pager" />
        </asp:GridView>
        </div>

    </div>

</asp:Content>
