<%@ Page Title="Marks Objection - KPSC Examination Portal" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Objection.aspx.cs" Inherits="KPSCExamPortal.Marks.Objection" %>
<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <link href="<%: ResolveUrl("~/css/Objection.css?v=5") %>" rel="stylesheet" />

    <script type="text/javascript">
        // Remarks counter for the remarks box in each paper card.
        function updateRemarksCount(txt) {
            if (txt.value.length > 700) {
                txt.value = txt.value.substring(0, 700);
            }

            var counter = txt.parentNode.querySelector('.remarks-counter');
            if (counter) {
                counter.innerText = txt.value.length + " / 700 characters";
            }
        }

        document.addEventListener("DOMContentLoaded", function () {
            var boxes = document.querySelectorAll('.paper-card textarea');
            for (var i = 0; i < boxes.length; i++) {
                updateRemarksCount(boxes[i]);
            }
        });
    </script>

    <div class="candidate-panel">

        <div class="panel-heading">
            MARKS OBJECTION
        </div>

        <div class="candidate-body">
            <p class="paper-intro">
                You can file one objection for each paper. Once submitted, an objection cannot be changed or deleted.
            </p>

            <%-- Shown when the objection window is not open --%>
            <asp:Label ID="lblNotice" runat="server" Visible="false"
                CssClass="alert alert-warning d-block" />

            <asp:Label ID="lblNoPapers" runat="server" Visible="false"
                CssClass="alert alert-info d-block"
                Text="No papers are set up for this examination." />

            <%-- One card per paper: the objection filed, or a form to file one --%>
            <asp:Repeater ID="rptPapers" runat="server" OnItemCommand="rptPapers_ItemCommand">
                <ItemTemplate>
                    <div class='paper-card <%# IsFiled(Container.DataItem) ? "is-filed" : "" %>'>

                        <div class="paper-card-head">
                            <div>
                                <span class="paper-title"><%#: Eval("SubjectDescription") %></span>
                                <span class="paper-meta">
                                    Paper <%#: Eval("PaperCode") %>
                                    <%# Eval("VersionCode") is DBNull ? "" : "&middot; Version " + HttpUtility.HtmlEncode(Eval("VersionCode")) %>
                                </span>
                            </div>
                            <span class='status-badge <%# StatusCss(Container.DataItem) %>'><%# StatusText(Container.DataItem) %></span>
                        </div>

                        <div class="paper-card-body">

                            <%-- Filed: what the candidate submitted --%>
                            <asp:PlaceHolder ID="phFiled" runat="server" Visible='<%# IsFiled(Container.DataItem) %>'>
                                <div class="filed-grid">
                                    <div class="filed-remarks">
                                        <div class="filed-label">Remarks</div>
                                        <div class="filed-value"><%#: Eval("Remarks") %></div>
                                    </div>
                                    <div>
                                        <div class="filed-label">Supporting Document</div>
                                        <div class="filed-value">
                                            <asp:HyperLink ID="lnkDocument" runat="server"
                                                Target="_blank"
                                                Visible='<%# !(Eval("FileName") is DBNull) %>'
                                                Text='<%#: Eval("FileName") %>'
                                                NavigateUrl='<%# KPSCExamPortal.Security.DocumentReferences.MarksUrl(Session, Eval("PaperCode")) %>' />
                                            <%# Eval("FileName") is DBNull ? "None" : "" %>
                                        </div>
                                    </div>
                                    <div>
                                        <div class="filed-label">Submitted</div>
                                        <div class="filed-value"><%# Eval("SubmittedDate", "{0:dd-MM-yyyy HH:mm}") %></div>
                                    </div>
                                </div>
                            </asp:PlaceHolder>

                            <%-- Not filed, window open: the form for this paper --%>
                            <asp:PlaceHolder ID="phForm" runat="server" Visible='<%# !IsFiled(Container.DataItem) && WindowOpen %>'>
                                <div class="row">
                                    <div class="col-md-7">
                                        <div class="field-group">
                                            <label class="form-label" for="<%# Container.FindControl("txtRemarks").ClientID %>">
                                                Remarks <span class="required">*</span>
                                            </label>
                                            <asp:TextBox ID="txtRemarks" runat="server" CssClass="form-control remarks-box"
                                                TextMode="MultiLine" MaxLength="700" Rows="4"
                                                oninput="updateRemarksCount(this)" />
                                            <small class="text-muted remarks-counter">0 / 700 characters</small>
                                        </div>
                                    </div>
                                    <div class="col-md-5">
                                        <div class="field-group">
                                            <label class="form-label" for="<%# Container.FindControl("fileDocs").ClientID %>">
                                                Supporting Document <span class="text-muted">(optional; PDF only, up to 1 MB)</span>
                                            </label>
                                            <asp:FileUpload ID="fileDocs" runat="server" CssClass="form-select" />
                                        </div>
                                        <div class="paper-submit">
                                            <asp:Button ID="btnSubmit" runat="server" Text="Submit"
                                                CssClass="btn btn-primary add-button"
                                                CommandName="Submit"
                                                CommandArgument='<%# Eval("PaperCode") %>'
                                                OnClientClick="return confirm('Once submitted, the objection for this paper cannot be changed or deleted. Submit now?');" />
                                        </div>
                                    </div>
                                </div>
                            </asp:PlaceHolder>

                            <%-- Not filed, window closed --%>
                            <asp:PlaceHolder ID="phClosed" runat="server" Visible='<%# !IsFiled(Container.DataItem) && !WindowOpen %>'>
                                <p class="paper-none">No objection was filed for this paper.</p>
                            </asp:PlaceHolder>

                        </div>
                    </div>
                </ItemTemplate>
            </asp:Repeater>
        </div>
    </div>
</asp:Content>
