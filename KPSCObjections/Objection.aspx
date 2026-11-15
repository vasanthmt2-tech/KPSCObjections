<%@ Page Title="" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Objection.aspx.cs" Inherits="KPSCObjections.Objection" %>
<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
     <link href="css/Objection.css" rel="stylesheet" />
    <div class="candidate-panel">

        <script type="text/javascript">

            function configureAnswerSelection() {

                var categoryDropdown = document.getElementById('<%= ddlCategory.ClientID %>');
        var checkboxList = document.getElementById('<%= cblObjectionAnswer.ClientID %>');

                if (!categoryDropdown || !checkboxList)
                    return;

                var categoryCode = categoryDropdown.value;

                var checkboxes = checkboxList.querySelectorAll('input[type="checkbox"]');

                checkboxes.forEach(function (checkbox) {

                    // Remove previously attached event
                    checkbox.onchange = null;

                    checkbox.onchange = function () {

                        // Category 2: Multiple options allowed
                        if (categoryCode === "2") {
                            return;
                        }

                        // Category 3: Only Grace allowed
                        if (categoryCode === "3") {
                            if (this.value !== "5") {
                                this.checked = false;
                            }
                            return;
                        }

                        // Other categories: Only one option allowed
                        if (this.checked) {

                            checkboxes.forEach(function (otherCheckbox) {

                                if (otherCheckbox !== checkbox) {
                                    otherCheckbox.checked = false;
                                }

                            });
                        }
                    };

                });
            }

            // Initialize after every ASP.NET postback
            if (typeof (Sys) !== 'undefined' && Sys.Application) {
                Sys.Application.add_load(configureAnswerSelection);
            } else {
                document.addEventListener('DOMContentLoaded', configureAnswerSelection);
            }
            function updateRemarksCount(txt) {

                if (txt.value.length > 700) {
                    txt.value = txt.value.substring(0, 700);
                }

                document.getElementById("remarksCounter").innerText =
                    txt.value.length + " / 700 characters";
            }

            document.addEventListener("DOMContentLoaded", function () {

                var remarks = document.getElementById(
            '<%= txtRemarks.ClientID %>'
        );

        if (remarks) {
            updateRemarksCount(remarks);
        }

            });

        </script>
            <!-- Section header -->
            <div class="panel-heading">
                OBJECTION DETAILS
            </div>

            <div class="candidate-body">
                <!-- Shown when the objection window is not open -->
                <asp:Label ID="lblNotice" runat="server" Visible="false"
                    CssClass="alert alert-warning d-block" />

                <!-- Shown while a payment is being verified -->
                <asp:Panel ID="pnlPending" runat="server" Visible="false"
                    CssClass="alert alert-info">
                    <asp:Label ID="lblPending" runat="server" />
                    <div class="mt-2">
                        <asp:Button ID="btnCheckPayment" runat="server"
                            Text="Check Payment Status"
                            CssClass="btn btn-primary btn-sm"
                            CausesValidation="false"
                            OnClick="btnCheckPayment_Click" />
                    </div>
                </asp:Panel>

                <div class="row">
                  <!-- Entry form: hidden when objections cannot be changed -->
                  <asp:Panel ID="pnlEntry" runat="server" CssClass="row">
                    <!-- LEFT COLUMN -->
                    <div class="col-md-6">
                        <!-- Subject Code -->
                        <div class="field-group">
                            <label class="form-label" for="<%= ddlSubjectCode.ClientID %>">Subject Code <span class="required">*</span></label>
                            <asp:DropDownList ID="ddlSubjectCode" runat="server" CssClass="form-select" OnSelectedIndexChanged="ddlSubjectCode_SelectedIndexChanged" AutoPostBack="true">
                              <%--  <asp:ListItem Text="570" Value="570" />
                                <asp:ListItem Text="571" Value="571" />
                                <asp:ListItem Text="572" Value="572" />--%>
                            </asp:DropDownList>
                        </div>
                       
                        <!-- Question Number -->
                        <div class="field-group">
                            <label class="form-label" for="<%= ddlQuestionNumber.ClientID %>">Question Number <span class="required">*</span></label>

                            <asp:DropDownList ID="ddlQuestionNumber" runat="server" CssClass="form-select" OnSelectedIndexChanged="ddlQuestionNumber_SelectedIndexChanged" AutoPostBack="true">
                              <%--  <asp:ListItem Text="570" Value="570" />
                                <asp:ListItem Text="571" Value="571" />
                                <asp:ListItem Text="572" Value="572" />--%>
                            </asp:DropDownList>
                        </div>
                        <!-- Categories -->
                        <div class="field-group">
                            <label class="form-label" for="<%= ddlCategory.ClientID %>">Category<span class="required">*</span></label>

                            <asp:DropDownList ID="ddlCategory" runat="server" CssClass="form-select" OnSelectedIndexChanged="ddlCategory_SelectedIndexChanged" AutoPostBack="true">
                              <%--  <asp:ListItem Text="570" Value="570" />
                                <asp:ListItem Text="571" Value="571" />
                                <asp:ListItem Text="572" Value="572" />--%>
                            </asp:DropDownList>
                        </div>
                        
                        <!-- Objection Remarks -->
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

                    <!-- RIGHT COLUMN -->
                    <div class="col-md-6">
                        
                         <!-- Version Code -->
                        <div class="field-group">
                            <label class="form-label" for="<%= ddlVersionCode.ClientID %>"> Version Code <span class="required">*</span></label>

                            <asp:DropDownList ID="ddlVersionCode" runat="server" CssClass="form-select" Enabled="false">
                               <%-- <asp:ListItem Text="A" Value="A" />
                                <asp:ListItem Text="B" Value="B" />
                                <asp:ListItem Text="C" Value="C" />--%>
                            </asp:DropDownList>
                        </div>

                        <!-- Candidate Name -->
                        <div class="field-group">
                            <label class="form-label" for="<%= txtKeyAnswer.ClientID %>">Key Answer <span class="required">*</span></label>

                            <asp:TextBox ID="txtKeyAnswer" runat="server" CssClass="form-control" ReadOnly="true" Text="" />
                        </div>
                        <!-- Objection Answer -->
                        <div class="field-group">
                            <label class="form-label" for="<%= cblObjectionAnswer.ClientID %>">Objection Answer<span class="required">*</span></label>
                            <asp:CheckBoxList ID="cblObjectionAnswer" runat="server"
                                RepeatDirection="Horizontal"
                                RepeatLayout="Flow"
                                CssClass="objection-options">

                                <asp:ListItem Text="1" Value="1" />
                                <asp:ListItem Text="2" Value="2" />
                                <asp:ListItem Text="3" Value="3" />
                                <asp:ListItem Text="4" Value="4" />
                                <asp:ListItem Text="Grace" Value="5" />
                            </asp:CheckBoxList>
                        </div>
                        <!-- File Upload -->
                        <div class="field-group">
                            <label class="form-label" for="<%= fileDocs.ClientID %>">Supporting Document <span class="text-muted">(optional; pdf only, less than 1 MB)</span></label>
                            <asp:FileUpload ID="fileDocs" runat="server" CssClass="form-select">
                              <%--  <asp:ListItem Text="570" Value="570" />
                                <asp:ListItem Text="571" Value="571" />
                                <asp:ListItem Text="572" Value="572" />--%>
                            </asp:FileUpload>
                             <%--<label class="form-label">Upload only pdf files less than 500KB </label>--%>
                        </div>
                    </div>
                    <div class="field-group">
                           <%-- <asp:Button ID="btnAdd" runat="server" Text="Add" OnClick="btnAdd_Click" />--%>
                        <div class="add-button-section">
                            <asp:Button ID="btnAdd" runat="server" Text ="Add" CssClass="btn btn-primary add-button" OnClick="btnAdd_Click" />
                        </div>
                    </div>
                  </asp:Panel>

                    <!-- Objection Details Grid -->
                    <div class="table-responsive mt-4">
                        <asp:GridView ID="gvObjections" runat="server"
                            AutoGenerateColumns="False"
                            CssClass="table table-bordered table-striped table-hover"
                            HeaderStyle-CssClass="table-dark"
                            DataKeyNames="SubjectCode,VersionCode,QuestionNo"
                            OnRowDeleting="gvObjections_RowDeleting"
                            EmptyDataText="No objection details added yet."
                            GridLines="None"
                            Width="100%">

                            <Columns>
                                <asp:BoundField DataField="SubjectDesc"
                                   HeaderText="Subject" />

                                <asp:BoundField DataField="SubjectCode"
                                   Visible="false" HeaderText="Subjectcode" />

                                <asp:BoundField DataField="VersionCode"
                                    HeaderText="Version Code" />

                                <asp:BoundField DataField="QuestionNo"
                                    HeaderText="Question Number" />

                                <asp:BoundField DataField="KeyAnswer"
                                    HeaderText="Key Answer" />

                                <asp:BoundField DataField="ObjectionAnswer"
                                    HeaderText="Objection Answer" />

                                <asp:BoundField DataField="Remarks"
                                    HeaderText="Remarks" />

                                <asp:TemplateField HeaderText="Document">
                                    <ItemTemplate>
                                        <asp:HyperLink ID="lnkDocument" runat="server"
                                            Target="_blank"
                                            Visible='<%# !(Eval("FileName") is DBNull) %>'
                                            Text='<%#: Eval("FileName") %>'
                                            NavigateUrl='<%# "~/ObjectionDocument.ashx?paper=" + HttpUtility.UrlEncode(Convert.ToString(Eval("SubjectCode"))) + "&q=" + Eval("QuestionNo") %>'>
                                        </asp:HyperLink>
                                    </ItemTemplate>
                                </asp:TemplateField>

                                <asp:BoundField DataField="Fee"
                                    HeaderText="Fee(Rs.)" />

                                <asp:TemplateField HeaderText="Action">
                                    <ItemTemplate>
                                        <asp:LinkButton ID="lnkDelete" runat="server"
                                            CommandName="Delete"
                                            Text="Delete"
                                            CssClass="btn btn-danger btn-sm"
                                            OnClientClick="return confirm('Are you sure you want to delete this row?');">
                                        </asp:LinkButton>
                                    </ItemTemplate>
                                </asp:TemplateField>
                            </Columns>

                        </asp:GridView> 
                    </div>
                    <div class="field-group">
                        <div class="add-button-section">
                            <asp:Button ID="btnPayNow" runat="server" Visible="false" Text ="Pay Now" CssClass="btn btn-primary add-button" OnClick="btnPayNow_Click" />
                        </div>
                    </div>
                </div>
            </div>
        </div>
</asp:Content>
