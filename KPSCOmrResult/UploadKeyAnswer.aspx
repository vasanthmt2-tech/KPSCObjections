<%@ Page Title="Upload Key Answers - OMR Result Portal" Language="C#" MasterPageFile="~/Admin.Master" AutoEventWireup="true" CodeBehind="UploadKeyAnswer.aspx.cs" Inherits="KPSCOmrResult.UploadKeyAnswer" %>
<asp:Content ID="Content1"
    ContentPlaceHolderID="MainContent"
    runat="server">

    <div class="container mt-4">

        <h2 class="h3">Upload Final Key</h2>
        <p class="text-muted">The final key answers shown to candidates with their result. They are kept separately from the answer key objection portal's provisional key.</p>

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
            <label for="<%= ddlSubjectCode.ClientID %>">Subject Code</label>
            <asp:DropDownList ID="ddlSubjectCode"
                runat="server"
                CssClass="form-control">
            </asp:DropDownList>
        </div>

        <div class="form-group mb-3">
            <label for="<%= fuKeyAnswers.ClientID %>">Select Excel File (.xlsx)</label>
            <asp:FileUpload ID="fuKeyAnswers"
                runat="server"
                CssClass="form-control"
                accept=".xlsx" />
            <small class="text-muted">
                Same layout as the answer key file: <strong>M, Key Answer, Set A to Set L</strong> for P1
                or <strong>Set M to Set X</strong> for P2. Key Answer: 1 to 4, several accepted answers such as
                <strong>2 or 3</strong> (also 2/3, 2,3 or 2 and 3), or <strong>GRACE</strong> (marks for every candidate).
            </small>
        </div>

        <asp:Button ID="btnUpload"
            runat="server"
            Text="Upload Key Answers"
            CssClass="btn btn-primary"
            OnClick="btnUpload_Click" />

        <div class="mt-3 mb-3">
            <asp:Label ID="lblMessage"
                runat="server">
            </asp:Label>
        </div>

        <hr />

        <h3 class="h4 mt-4">Uploaded Key Answer Documents</h3>

        <%-- Scrolls inside its own box on narrow screens. --%>
        <div class="table-responsive">
        <asp:GridView ID="gvUploadedDocuments"
    runat="server"
    AutoGenerateColumns="False"
    CssClass="table table-bordered table-striped table-hover"
    GridLines="None"
    DataKeyNames="DocumentId"
    EmptyDataText="No uploaded documents found."
    OnRowCommand="gvUploadedDocuments_RowCommand">

            <Columns>

                <asp:TemplateField HeaderText="Sl. No.">
                    <ItemTemplate>
                        <%# Container.DataItemIndex + 1 %>
                    </ItemTemplate>
                </asp:TemplateField>

                <asp:BoundField
                    DataField="ExamDesc"
                    HeaderText="Examination" />

                <asp:BoundField
                    DataField="SubjectCode"
                    HeaderText="Subject Code" />

                <asp:BoundField
                    DataField="FileName"
                    HeaderText="Document Name" />

                <asp:BoundField
                    DataField="UploadedOn"
                    HeaderText="Uploaded Date"
                    DataFormatString="{0:dd-MM-yyyy hh:mm tt}" />

                <asp:TemplateField HeaderText="Download">
                    <ItemTemplate>
                        <asp:LinkButton ID="lnkDownload"
                            runat="server"
                            Text="Download"
                            CssClass="btn btn-success btn-sm"
                            CommandName="DownloadFile"
                            CommandArgument='<%# Eval("DocumentId") %>'>
                        </asp:LinkButton>
                    </ItemTemplate>
                </asp:TemplateField>

                <asp:TemplateField HeaderText="Delete">
                    <ItemTemplate>
                        <asp:LinkButton ID="lnkDelete"
                            runat="server"
                            Text="Delete"
                            CssClass="btn btn-danger btn-sm"
                            CommandName="DeleteFile"
                            CommandArgument='<%# Eval("DocumentId") %>'
                            OnClientClick="return confirm('Are you sure you want to delete this document?');">
                        </asp:LinkButton>
                    </ItemTemplate>
                </asp:TemplateField>

            </Columns>

            <HeaderStyle CssClass="table-dark" />
        </asp:GridView>
        </div>

    </div>

</asp:Content>


