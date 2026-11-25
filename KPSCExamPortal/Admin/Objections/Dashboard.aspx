<%@ Page Title="Dashboard - Answer Key Objection Portal" Language="C#" MasterPageFile="~/Admin.Master" AutoEventWireup="true" CodeBehind="Dashboard.aspx.cs" Inherits="KPSCExamPortal.Admin.Objections.Dashboard" %>
<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">

    <style>
        .dash-card { border: 1px solid #dee2e6; border-radius: .5rem; padding: 1rem 1.1rem; background: #fff; height: 100%; }
        .dash-label { font-size: .8rem; font-weight: 600; text-transform: uppercase; letter-spacing: .03em; color: #6c757d; }
        .dash-value { font-size: 1.9rem; font-weight: 700; line-height: 1.2; margin-top: .25rem; font-variant-numeric: tabular-nums; }
        .dash-sub { font-size: .85rem; color: #6c757d; margin-top: .2rem; }
        .dash-accent { border-top: 4px solid #0d6efd; }
        .dash-ok { border-top: 4px solid #198754; }
        .dash-warn { border-top: 4px solid #fd7e14; }
        .dash-muted { border-top: 4px solid #adb5bd; }
        .dash-section { font-size: 1.05rem; font-weight: 700; margin: 1.75rem 0 .6rem; }
        .dash-table td, .dash-table th { vertical-align: middle; }
        .dash-table .num { text-align: right; font-variant-numeric: tabular-nums; white-space: nowrap; }
        .dash-bar { height: .5rem; background: #e9ecef; border-radius: .25rem; min-width: 80px; }
        .dash-bar > span { display: block; height: 100%; background: #0d6efd; border-radius: .25rem; }
        .dash-table tfoot td { font-weight: 700; background: #f1f3f5; }
    </style>

    <div class="container-fluid px-4 mt-4 mb-5">

        <div class="d-flex flex-wrap align-items-center gap-3 mb-1">
            <h2 class="h3 mb-0 me-auto">Dashboard</h2>

            <asp:Panel ID="pnlExam" runat="server" CssClass="d-flex align-items-center gap-2">
                <label class="form-label mb-0" for="<%= ddlExam.ClientID %>">Examination</label>
                <asp:DropDownList ID="ddlExam" runat="server" CssClass="form-select form-select-sm"
                    AutoPostBack="true" OnSelectedIndexChanged="ddlExam_SelectedIndexChanged" />
            </asp:Panel>

            <asp:LinkButton ID="btnRefresh" runat="server" CssClass="btn btn-sm btn-outline-primary"
                CausesValidation="false" OnClick="btnRefresh_Click" ToolTip="Refresh the figures">
                <i class="bi bi-arrow-clockwise"></i> Refresh
            </asp:LinkButton>
        </div>

        <asp:Label ID="lblNoExam" runat="server" CssClass="text-danger fw-bold" Visible="false"
            Text="No examination has been set up yet." />

        <asp:Panel ID="pnlContent" runat="server">

            <p class="text-muted mb-3">
                <asp:Literal ID="litExamName" runat="server" /> ·
                <asp:Label ID="lblAsOf" runat="server" /> ·
                "Submitted" means the objection fee was paid successfully.
            </p>

            <%-- Summary --%>
            <div class="row g-3">
                <div class="col-12 col-sm-6 col-xl-4 col-xxl-2">
                    <div class="dash-card dash-accent">
                        <div class="dash-label">Objections submitted</div>
                        <div class="dash-value"><asp:Literal ID="litSubmitted" runat="server" /></div>
                        <div class="dash-sub"><asp:Literal ID="litSubmittedSub" runat="server" /></div>
                    </div>
                </div>
                <div class="col-12 col-sm-6 col-xl-4 col-xxl-2">
                    <div class="dash-card dash-ok">
                        <div class="dash-label">Fee collected</div>
                        <div class="dash-value"><asp:Literal ID="litCollected" runat="server" /></div>
                        <div class="dash-sub"><asp:Literal ID="litCollectedSub" runat="server" /></div>
                    </div>
                </div>
                <div class="col-12 col-sm-6 col-xl-4 col-xxl-2">
                    <div class="dash-card dash-warn">
                        <div class="dash-label">Payment being verified</div>
                        <div class="dash-value"><asp:Literal ID="litPending" runat="server" /></div>
                        <div class="dash-sub"><asp:Literal ID="litPendingSub" runat="server" /></div>
                    </div>
                </div>
                <div class="col-12 col-sm-6 col-xl-4 col-xxl-2">
                    <div class="dash-card dash-muted">
                        <div class="dash-label">Not paid</div>
                        <div class="dash-value"><asp:Literal ID="litNotPaid" runat="server" /></div>
                        <div class="dash-sub"><asp:Literal ID="litNotPaidSub" runat="server" /></div>
                    </div>
                </div>
                <div class="col-12 col-sm-6 col-xl-4 col-xxl-2">
                    <div class="dash-card dash-muted">
                        <div class="dash-label">Candidates uploaded</div>
                        <div class="dash-value"><asp:Literal ID="litCandidates" runat="server" /></div>
                        <div class="dash-sub"><asp:Literal ID="litCandidatesSub" runat="server" /></div>
                    </div>
                </div>
                <div class="col-12 col-sm-6 col-xl-4 col-xxl-2">
                    <div class="dash-card dash-muted">
                        <div class="dash-label">Objection window</div>
                        <asp:Panel ID="pnlWindowValue" runat="server" CssClass="dash-value">
                            <asp:Literal ID="litWindow" runat="server" />
                        </asp:Panel>
                        <div class="dash-sub"><asp:Literal ID="litWindowSub" runat="server" /></div>
                        <asp:HyperLink ID="lnkEditWindow" runat="server" CssClass="small"
                            Text="Change the window" />
                    </div>
                </div>
            </div>

            <%-- Subject-wise --%>
            <div class="dash-section">Objections received – subject-wise</div>
            <div class="table-responsive">
                <asp:Repeater ID="rptSubjects" runat="server">
                    <HeaderTemplate>
                        <table class="table table-bordered table-hover dash-table mb-0">
                            <thead class="table-dark">
                                <tr>
                                    <th>Subject Code</th>
                                    <th>Subject</th>
                                    <th>Paper</th>
                                    <th class="num">Submitted Objections</th>
                                    <th>Share</th>
                                    <th class="num">Candidates</th>
                                    <th class="num">Questions Objected</th>
                                    <th class="num">Payment Being Verified</th>
                                    <th class="num">Not Paid</th>
                                </tr>
                            </thead>
                            <tbody>
                    </HeaderTemplate>
                    <ItemTemplate>
                                <tr>
                                    <td><%#: Eval("SubjectCode") %></td>
                                    <td><%#: Eval("SubjectDescription") %></td>
                                    <td><%#: Eval("PaperCode") %></td>
                                    <td class="num fw-bold"><%#: Num(Eval("Submitted")) %></td>
                                    <td>
                                        <div class="d-flex align-items-center gap-2">
                                            <div class="dash-bar flex-grow-1"><span style="width:<%#: BarWidth(Eval("Submitted"), TotalSubmitted) %>%"></span></div>
                                            <small class="text-muted"><%#: Percent(Eval("Submitted"), TotalSubmitted) %></small>
                                        </div>
                                    </td>
                                    <td class="num"><%#: Num(Eval("Candidates")) %></td>
                                    <td class="num"><%#: Num(Eval("Questions")) %></td>
                                    <td class="num"><%#: Num(Eval("Pending")) %></td>
                                    <td class="num"><%#: Num(Eval("NotPaid")) %></td>
                                </tr>
                    </ItemTemplate>
                    <FooterTemplate>
                            </tbody>
                            <tfoot>
                                <tr>
                                    <td colspan="3">Total</td>
                                    <td class="num"><%#: Num(TotalSubmitted) %></td>
                                    <td></td>
                                    <td class="num" title="Each candidate counted once"><%#: Num(TotalCandidates) %></td>
                                    <td class="num"><%#: Num(TotalQuestions) %></td>
                                    <td class="num"><%#: Num(TotalPending) %></td>
                                    <td class="num"><%#: Num(TotalNotPaid) %></td>
                                </tr>
                            </tfoot>
                        </table>
                    </FooterTemplate>
                </asp:Repeater>
            </div>
            <div class="form-text">
                Questions objected counts each question once across all question paper versions.
                Candidates in the total row counts each candidate once, even if they objected in several subjects.
            </div>

            <div class="row g-4">

                <%-- Most-objected questions --%>
                <div class="col-12 col-xl-7">
                    <div class="dash-section">Most-objected questions (top 10, submitted)</div>
                    <div class="table-responsive">
                        <asp:Repeater ID="rptTopQuestions" runat="server">
                            <HeaderTemplate>
                                <table class="table table-bordered table-striped dash-table mb-0">
                                    <thead class="table-dark">
                                        <tr>
                                            <th>Paper</th>
                                            <th>Question</th>
                                            <th>Key Answer</th>
                                            <th class="num">Objections</th>
                                            <th class="num">Candidates</th>
                                            <th>Most Suggested Answer</th>
                                        </tr>
                                    </thead>
                                    <tbody>
                            </HeaderTemplate>
                            <ItemTemplate>
                                        <tr>
                                            <td><%#: Eval("PaperCode") %></td>
                                            <td><%#: QuestionLabel(Eval("MasterQ"), Eval("VersionCode"), Eval("VersionQ")) %></td>
                                            <td><%#: Eval("KeyAnswer") %></td>
                                            <td class="num fw-bold"><%#: Num(Eval("Objections")) %></td>
                                            <td class="num"><%#: Num(Eval("Candidates")) %></td>
                                            <td><%#: Eval("TopAnswer") %> <small class="text-muted">(<%#: Num(Eval("TopAnswerCount")) %>)</small></td>
                                        </tr>
                            </ItemTemplate>
                            <FooterTemplate>
                                    </tbody>
                                </table>
                            </FooterTemplate>
                        </asp:Repeater>
                    </div>
                    <asp:Label ID="lblNoTopQuestions" runat="server" CssClass="text-muted" Visible="false"
                        Text="No submitted objections yet." />
                    <div class="form-text">Question numbers are those of the master key (version-independent).</div>
                </div>

                <%-- Category-wise --%>
                <div class="col-12 col-xl-5">
                    <div class="dash-section">Objections by category (submitted)</div>
                    <div class="table-responsive">
                        <asp:Repeater ID="rptCategories" runat="server">
                            <HeaderTemplate>
                                <table class="table table-bordered dash-table mb-0">
                                    <thead class="table-dark">
                                        <tr>
                                            <th>Category</th>
                                            <th class="num">Objections</th>
                                            <th>Share</th>
                                        </tr>
                                    </thead>
                                    <tbody>
                            </HeaderTemplate>
                            <ItemTemplate>
                                        <tr>
                                            <td><%#: Eval("CategoryDescription") %></td>
                                            <td class="num fw-bold"><%#: Num(Eval("Objections")) %></td>
                                            <td>
                                                <div class="d-flex align-items-center gap-2">
                                                    <div class="dash-bar flex-grow-1"><span style="width:<%#: BarWidth(Eval("Objections"), CategoryTotal) %>%"></span></div>
                                                    <small class="text-muted"><%#: Percent(Eval("Objections"), CategoryTotal) %></small>
                                                </div>
                                            </td>
                                        </tr>
                            </ItemTemplate>
                            <FooterTemplate>
                                    </tbody>
                                </table>
                            </FooterTemplate>
                        </asp:Repeater>
                    </div>
                </div>
            </div>

            <%-- Day-wise --%>
            <div class="dash-section">Day-wise submissions</div>
            <div class="table-responsive">
                <asp:Repeater ID="rptDaily" runat="server">
                    <HeaderTemplate>
                        <table class="table table-bordered table-striped dash-table mb-0">
                            <thead class="table-dark">
                                <tr>
                                    <th>Date</th>
                                    <th class="num">Objections Submitted</th>
                                    <th class="num">Candidates</th>
                                    <th class="num">Fee Collected</th>
                                </tr>
                            </thead>
                            <tbody>
                    </HeaderTemplate>
                    <ItemTemplate>
                                <tr>
                                    <td><%#: Day(Eval("Day")) %></td>
                                    <td class="num fw-bold"><%#: Num(Eval("Objections")) %></td>
                                    <td class="num"><%#: Num(Eval("Candidates")) %></td>
                                    <td class="num"><%#: Money(Eval("Collected")) %></td>
                                </tr>
                    </ItemTemplate>
                    <FooterTemplate>
                            </tbody>
                        </table>
                    </FooterTemplate>
                </asp:Repeater>
            </div>
            <asp:Label ID="lblNoDaily" runat="server" CssClass="text-muted" Visible="false"
                Text="No submitted objections yet." />

        </asp:Panel>
    </div>

</asp:Content>
