<%@ Page Title="Upload Candidates - KPSC Examination Portal" Language="C#" MasterPageFile="~/Admin.Master" AutoEventWireup="true" CodeBehind="UploadCandidates.aspx.cs" Inherits="KPSCExamPortal.Admin.UploadCandidates" %>
<asp:Content ID="Content1"
    ContentPlaceHolderID="MainContent"
    runat="server">

    <div class="container mt-4">

        <h2 class="h3">Upload Candidates</h2>
        <p class="text-muted">Candidates for the marks objection portal. They are kept separately from the answer key objection portal's candidates.</p>

        <div class="form-group mb-3">
            <label for="<%= ddlExamCode.ClientID %>">Examination</label>
            <asp:DropDownList ID="ddlExamCode"
                runat="server"
                CssClass="form-control"
                AutoPostBack="true"
                OnSelectedIndexChanged="ddlExamCode_SelectedIndexChanged">
            </asp:DropDownList>
        </div>

        <%-- Step 1: candidate details --%>
        <h3 class="h5 mt-4">1. Candidate details</h3>

        <div class="form-group mb-3">
            <label for="<%= fuCandidates.ClientID %>">Select Excel File (.xlsx)</label>
            <asp:FileUpload ID="fuCandidates"
                runat="server"
                CssClass="form-control"
                accept=".xlsx" />
            <small class="text-muted">
                First sheet, with these five columns in this order in row 1:
                <strong>Application Number, Hall ticket No, Candidate Name, Date of Birth, Mobile No</strong>.
                Hall ticket No is the registration number candidates log in with: up to 10 letters or digits.
                Date of Birth is required (for example 20-10-2001). Mobile No (10 digits) is required: the login OTP is sent to it.
                Maximum file size 10 MB; split a longer list into several files.
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

        <%-- Step 2: version codes and barcodes of the candidates uploaded above --%>
        <h3 class="h5 mt-4">2. Version codes</h3>

        <div class="form-group mb-3">
            <label for="<%= fuVersions.ClientID %>">Select Excel File (.xlsx)</label>
            <asp:FileUpload ID="fuVersions"
                runat="server"
                CssClass="form-control"
                accept=".xlsx" />
            <small class="text-muted">
                First sheet, with these five columns in this order in row 1:
                <strong>S.No, RegNo, Version, Barcode, Paper</strong>, one row per candidate per paper.
                RegNo must already be uploaded in step 1. Paper: P1 or P2.
                Version: one letter A to L for P1, M to X for P2. Barcode: up to 20 letters or digits.
                Uploading a candidate's paper again replaces its version code and barcode.
            </small>
        </div>

        <asp:Button ID="btnUploadVersions"
            runat="server"
            Text="Upload Version Codes"
            CssClass="btn btn-primary"
            OnClick="btnUploadVersions_Click" />

        <div class="mt-3 mb-3">
            <asp:Label ID="lblVersionMessage"
                runat="server">
            </asp:Label>
        </div>

        <hr />

        <h3 class="h4 mt-4">Candidates</h3>

        <%-- Shown once an examination is selected --%>
        <asp:Panel ID="pnlCandidates" runat="server" Visible="false">

            <div class="row g-2 align-items-center mb-3">
                <div class="col-auto">
                    <asp:TextBox ID="txtSearch"
                        runat="server"
                        CssClass="form-control"
                        MaxLength="50"
                        placeholder="Search RegNo, Application No. or Name" />
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
                        OnClientClick="return confirm('Delete ALL candidates of this examination? Candidates who have submitted objections are kept.');" />
                </div>
            </div>

            <div class="table-responsive">
            <asp:GridView ID="gvCandidates"
                runat="server"
                AutoGenerateColumns="False"
                CssClass="table table-bordered table-striped table-hover"
                GridLines="None"
                DataKeyNames="RegNo"
                AllowPaging="True"
                AllowCustomPaging="True"
                PageSize="50"
                EmptyDataText="No candidates found."
                OnPageIndexChanging="gvCandidates_PageIndexChanging">

                <Columns>

                    <%-- Only the page shown is read (see BindCandidates); SlNo counts across pages. --%>
                    <asp:BoundField DataField="SlNo" HeaderText="Sl. No." />

                    <asp:BoundField DataField="Applno" HeaderText="Application No." />
                    <asp:BoundField DataField="RegNo" HeaderText="RegNo" />
                    <asp:BoundField DataField="Name" HeaderText="Name" />
                    <asp:BoundField DataField="Dob" HeaderText="Date of Birth" />
                    <asp:BoundField DataField="MobileNo" HeaderText="Mobile No" />
                    <asp:BoundField DataField="P1VersionCode" HeaderText="P1 Version" />
                    <asp:BoundField DataField="P1Barcode" HeaderText="P1 Barcode" />
                    <asp:BoundField DataField="P2VersionCode" HeaderText="P2 Version" />
                    <asp:BoundField DataField="P2Barcode" HeaderText="P2 Barcode" />

                </Columns>

                <HeaderStyle CssClass="table-dark" />
                <PagerStyle CssClass="grid-pager" />
            </asp:GridView>
            </div>

        </asp:Panel>

        <asp:Label ID="lblSelectExam" runat="server" CssClass="text-muted"
            Text="Select an examination to see its candidates." />

    </div>

</asp:Content>
