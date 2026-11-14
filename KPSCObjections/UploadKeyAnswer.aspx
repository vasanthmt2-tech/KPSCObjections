<%@ Page Title="" Language="C#" MasterPageFile="~/Admin.Master" AutoEventWireup="true" CodeBehind="UploadKeyAnswer.aspx.cs" Inherits="KPSCObjections.UploadKeyAnswer" %>
<asp:Content ID="Content1"
    ContentPlaceHolderID="MainContent"
    runat="server">

    <div class="container mt-4">

        <h3>Upload Key Answers</h3>

        <div class="form-group mb-3">
            <label>Exam Code</label>
            <asp:DropDownList ID="ddlExamCode"
                runat="server"
                CssClass="form-control"
                AutoPostBack="true"
                OnSelectedIndexChanged="ddlExamCode_SelectedIndexChanged">
            </asp:DropDownList>
        </div>

        <div class="form-group mb-3">
            <label>Subject Code</label>
            <asp:DropDownList ID="ddlSubjectCode"
                runat="server"
                CssClass="form-control">
            </asp:DropDownList>
        </div>

        <div class="form-group mb-3">
            <label>Select Excel File (.xlsx)</label>
            <asp:FileUpload ID="fuKeyAnswers"
                runat="server"
                CssClass="form-control"
                accept=".xlsx" />
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

        <h4 class="mt-4">Uploaded Key Answer Documents</h4>

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

               <%-- <asp:BoundField
                    DataField="ExamCode"
                    HeaderText="Exam Code" />--%>

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

</asp:Content>


