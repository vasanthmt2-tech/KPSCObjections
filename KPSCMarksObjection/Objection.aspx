<%@ Page Title="File Objection - Marks Objection Portal" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Objection.aspx.cs" Inherits="KPSCMarksObjection.Objection" %>
<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
     <link href="css/Objection.css?v=2" rel="stylesheet" />
    <div class="candidate-panel">

        <script type="text/javascript">
            function updateRemarksCount(txt) {

                if (txt.value.length > 700) {
                    txt.value = txt.value.substring(0, 700);
                }

                document.getElementById("remarksCounter").innerText =
                    txt.value.length + " / 700 characters";
            }

            document.addEventListener("DOMContentLoaded", function () {

                var remarks = document.getElementById('<%= txtRemarks.ClientID %>');

                if (remarks) {
                    updateRemarksCount(remarks);
                }
            });
        </script>
            <%-- Section header --%>
            <div class="panel-heading">
                OBJECTION DETAILS
            </div>

            <div class="candidate-body">
                <%-- Shown when the objection window is not open --%>
                <asp:Label ID="lblNotice" runat="server" Visible="false"
                    CssClass="alert alert-warning d-block" />

                <div class="row">
                  <%-- Entry form: hidden when the window is closed --%>
                  <asp:Panel ID="pnlEntry" runat="server" CssClass="row">
                    <%-- LEFT COLUMN --%>
                    <div class="col-md-6">
                        <%-- Subject Code --%>
                        <div class="field-group">
                            <label class="form-label" for="<%= ddlSubjectCode.ClientID %>">Subject Code <span class="required">*</span></label>
                            <asp:DropDownList ID="ddlSubjectCode" runat="server" CssClass="form-select" OnSelectedIndexChanged="ddlSubjectCode_SelectedIndexChanged" AutoPostBack="true">
                            </asp:DropDownList>
                        </div>

                        <%-- Objection Remarks --%>
                        <div class="field-group">
                            <label class="form-label" for="<%= txtRemarks.ClientID %>">
                                Remarks <span class="required">*</span>
                            </label>

                            <asp:TextBox ID="txtRemarks" runat="server" CssClass="form-control" TextMode="MultiLine" MaxLength="700" Rows="5" oninput="updateRemarksCount(this)">
                            </asp:TextBox>

                            <small id="remarksCounter" class="text-muted">
                                0 / 700 characters
                            </small>
                        </div>
                    </div>

                    <%-- RIGHT COLUMN --%>
                    <div class="col-md-6">

                         <%-- Version Code: the candidate's own, shown for information --%>
                        <div class="field-group">
                            <label class="form-label" for="<%= ddlVersionCode.ClientID %>"> Version Code</label>

                            <asp:DropDownList ID="ddlVersionCode" runat="server" CssClass="form-select" Enabled="false">
                            </asp:DropDownList>
                        </div>

                        <%-- File Upload --%>
                        <div class="field-group">
                            <label class="form-label" for="<%= fileDocs.ClientID %>">Supporting Document <span class="text-muted">(optional; PDF only, up to 1 MB)</span></label>
                            <asp:FileUpload ID="fileDocs" runat="server" CssClass="form-select" />
                        </div>
                    </div>
                    <div class="field-group">
                        <div class="add-button-section">
                            <asp:Button ID="btnAdd" runat="server" Text="Submit" CssClass="btn btn-primary add-button"
                                OnClick="btnAdd_Click"
                                OnClientClick="return confirm('Once submitted, the objection cannot be changed or deleted. Submit now?');" />
                        </div>
                    </div>
                  </asp:Panel>

                    <%-- Submitted objections --%>
                    <div class="table-responsive mt-4">
                        <asp:GridView ID="gvObjections" runat="server"
                            AutoGenerateColumns="False"
                            CssClass="table table-bordered table-striped table-hover"
                            HeaderStyle-CssClass="table-dark"
                            EmptyDataText="No objections submitted yet."
                            GridLines="None"
                            Width="100%">

                            <Columns>
                                <asp:BoundField DataField="SubjectDesc"
                                   HeaderText="Subject" />

                                <asp:BoundField DataField="VersionCode"
                                    HeaderText="Version Code" />

                                <asp:BoundField DataField="Remarks"
                                    HeaderText="Remarks" />

                                <asp:TemplateField HeaderText="Document">
                                    <ItemTemplate>
                                        <asp:HyperLink ID="lnkDocument" runat="server"
                                            Target="_blank"
                                            Visible='<%# !(Eval("FileName") is DBNull) %>'
                                            Text='<%#: Eval("FileName") %>'
                                            NavigateUrl='<%# KPSCMarksObjection.Security.DocumentReferences.MarksUrl(Session, Eval("SubjectCode")) %>'>
                                        </asp:HyperLink>
                                    </ItemTemplate>
                                </asp:TemplateField>

                                <asp:BoundField DataField="SubmittedDate"
                                    HeaderText="Submitted Date"
                                    DataFormatString="{0:dd-MM-yyyy HH:mm}" />
                            </Columns>

                        </asp:GridView>
                    </div>
                </div>
            </div>
        </div>
</asp:Content>
