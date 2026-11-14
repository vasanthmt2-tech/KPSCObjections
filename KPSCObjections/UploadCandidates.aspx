<%@ Page Title="" Language="C#" MasterPageFile="~/Admin.Master" AutoEventWireup="true" CodeBehind="UploadCandidates.aspx.cs" Inherits="KPSCObjections.UploadCandidates" %>
<asp:Content ID="Content1"
    ContentPlaceHolderID="MainContent"
    runat="server">

    <div class="container mt-4">

        <h3>Upload Candidates</h3>

        <div class="form-group mb-3">
            <label>Examination</label>
            <asp:DropDownList ID="ddlExamCode"
                runat="server"
                CssClass="form-control"
                AutoPostBack="true"
                OnSelectedIndexChanged="ddlExamCode_SelectedIndexChanged">
            </asp:DropDownList>
        </div>

        <div class="form-group mb-3">
            <label>Select Excel File (.xlsx)</label>
            <asp:FileUpload ID="fuCandidates"
                runat="server"
                CssClass="form-control"
                accept=".xlsx" />
            <small class="text-muted">
                First sheet, with these seven columns in this order in row 1:
                <strong>Applno, RegNo, Name, Dob, MobileNo, P1VersionCode, P2VersionCode</strong>.
                RegNo: up to 10 letters or digits. MobileNo: 10 digits.
                Version codes: one letter A to L. Maximum file size 5 MB.
            </small>
        </div>

        <asp:Button ID="btnUpload"
            runat="server"
            Text="Upload Candidates"
            CssClass="btn btn-primary"
            OnClick="btnUpload_Click" />

        <div class="mt-3 mb-3">
            <asp:Label ID="lblMessage"
                runat="server">
            </asp:Label>
        </div>

        <hr />

        <h4 class="mt-4">Candidates</h4>

        <%-- Shown once an examination is selected --%>
        <asp:Panel ID="pnlCandidates" runat="server" Visible="false">

            <div class="row g-2 align-items-center mb-3">
                <div class="col-auto">
                    <asp:TextBox ID="txtSearch"
                        runat="server"
                        CssClass="form-control"
                        MaxLength="50"
                        placeholder="Search RegNo, Applno or Name" />
                </div>
                <div class="col-auto">
                    <asp:Button ID="btnSearch"
                        runat="server"
                        Text="Search"
                        CssClass="btn btn-outline-primary"
                        CausesValidation="false"
                        OnClick="btnSearch_Click" />
                </div>
                <div class="col">
                    <asp:Label ID="lblCount" runat="server" CssClass="text-muted" />
                </div>
                <div class="col-auto">
                    <asp:Button ID="btnDeleteAll"
                        runat="server"
                        Text="Delete All For This Examination"
                        CssClass="btn btn-outline-danger"
                        CausesValidation="false"
                        OnClick="btnDeleteAll_Click"
                        OnClientClick="return confirm('Delete ALL candidates of this examination? Candidates who have filed objections or made payments are kept.');" />
                </div>
            </div>

            <asp:GridView ID="gvCandidates"
                runat="server"
                AutoGenerateColumns="False"
                CssClass="table table-bordered table-striped table-hover"
                GridLines="None"
                DataKeyNames="RegNo"
                AllowPaging="True"
                PageSize="50"
                EmptyDataText="No candidates found."
                OnPageIndexChanging="gvCandidates_PageIndexChanging"
                OnRowCommand="gvCandidates_RowCommand">

                <Columns>

                    <asp:TemplateField HeaderText="Sl. No.">
                        <ItemTemplate>
                            <%# Container.DataItemIndex + 1 %>
                        </ItemTemplate>
                    </asp:TemplateField>

                    <asp:BoundField DataField="Applno" HeaderText="Applno" />
                    <asp:BoundField DataField="RegNo" HeaderText="RegNo" />
                    <asp:BoundField DataField="Name" HeaderText="Name" />
                    <asp:BoundField DataField="Dob" HeaderText="Dob" />
                    <asp:BoundField DataField="MobileNo" HeaderText="MobileNo" />
                    <asp:BoundField DataField="P1VersionCode" HeaderText="P1 Version" />
                    <asp:BoundField DataField="P2VersionCode" HeaderText="P2 Version" />

                    <asp:TemplateField HeaderText="Delete">
                        <ItemTemplate>
                            <asp:LinkButton ID="lnkDelete"
                                runat="server"
                                Text="Delete"
                                CssClass="btn btn-danger btn-sm"
                                CommandName="DeleteCandidate"
                                CommandArgument='<%# Eval("RegNo") %>'
                                OnClientClick="return confirm('Are you sure you want to delete this candidate?');">
                            </asp:LinkButton>
                        </ItemTemplate>
                    </asp:TemplateField>

                </Columns>

                <HeaderStyle CssClass="table-dark" />
                <PagerStyle CssClass="grid-pager" />
            </asp:GridView>

        </asp:Panel>

        <asp:Label ID="lblSelectExam" runat="server" CssClass="text-muted"
            Text="Select an examination to see its candidates." />

    </div>

</asp:Content>
