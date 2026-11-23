<%@ Page Title="Upload Results - OMR Result Portal" Language="C#" MasterPageFile="~/Admin.Master" AutoEventWireup="true" CodeBehind="UploadResults.aspx.cs" Inherits="KPSCOmrResult.UploadResults" %>
<asp:Content ID="Content1"
    ContentPlaceHolderID="MainContent"
    runat="server">

    <div class="container mt-4">

        <h2 class="h3">Upload Results</h2>
        <p class="text-muted">OMR results that candidates see after they log in. Upload one file per paper, or several smaller files: a candidate's paper uploaded again is replaced.</p>

        <div class="form-group mb-3">
            <label for="<%= ddlExamCode.ClientID %>">Examination</label>
            <asp:DropDownList ID="ddlExamCode"
                runat="server"
                CssClass="form-control"
                AutoPostBack="true"
                OnSelectedIndexChanged="ddlExamCode_SelectedIndexChanged">
            </asp:DropDownList>
        </div>

        <div class="form-group mb-3">
            <label for="<%= fuResults.ClientID %>">Select Excel File (.xlsx)</label>
            <asp:FileUpload ID="fuResults"
                runat="server"
                CssClass="form-control"
                accept=".xlsx" />
            <small class="text-muted">
                First sheet, with these columns in this order in row 1:
                <strong>S.No, RegNo, Version, Barcode, Paper, Dist, Centre, Q1 to Q100, Cr., InCr., NA., LeftBlank, MarksObt., Rank</strong>.
                Paper: P1 or P2. Version: A to L for P1, M to X for P2. Q1 to Q100: the answer the candidate marked
                (5 = not attempted; an empty cell = left blank). Maximum file size 50 MB; split a larger file
                (for example by district) and upload the parts one after the other.
            </small>
        </div>

        <asp:Button ID="btnUpload"
            runat="server"
            Text="Upload Results"
            CssClass="btn btn-primary"
            OnClick="btnUpload_Click" />

        <div class="mt-3 mb-3">
            <asp:Label ID="lblMessage" runat="server"></asp:Label>
        </div>

        <hr />

        <h3 class="h4 mt-4">Results uploaded</h3>

        <asp:Panel ID="pnlSummary" runat="server" Visible="false">
            <div class="table-responsive">
            <asp:GridView ID="gvSummary"
                runat="server"
                AutoGenerateColumns="False"
                CssClass="table table-bordered table-striped"
                GridLines="None"
                DataKeyNames="PaperCode"
                EmptyDataText="No results uploaded for this examination."
                OnRowCommand="gvSummary_RowCommand">
                <Columns>
                    <asp:BoundField DataField="PaperCode" HeaderText="Paper" />
                    <asp:BoundField DataField="SubjectDesc" HeaderText="Subject" />
                    <asp:BoundField DataField="Candidates" HeaderText="Candidates" DataFormatString="{0:N0}" />
                    <asp:BoundField DataField="NotRegistered" HeaderText="Not in candidate list" DataFormatString="{0:N0}" />
                    <asp:TemplateField HeaderText="">
                        <ItemTemplate>
                            <asp:LinkButton ID="lnkDelete" runat="server"
                                Text="Delete this paper's results"
                                CssClass="btn btn-outline-danger btn-sm"
                                CommandName="DeletePaper"
                                CommandArgument='<%# Eval("PaperCode") %>'
                                OnClientClick="return confirm('Delete ALL uploaded results of this paper? Candidates will no longer see them.');" />
                        </ItemTemplate>
                    </asp:TemplateField>
                </Columns>
                <HeaderStyle CssClass="table-dark" />
            </asp:GridView>
            </div>
            <small class="text-muted">"Not in candidate list": result rows whose registration number is not among the marks portal's candidates for this examination. Those candidates cannot log in to see the result.</small>
        </asp:Panel>

        <asp:Label ID="lblSelectExam" runat="server" CssClass="text-muted"
            Text="Select an examination to see its results." />

    </div>

</asp:Content>
