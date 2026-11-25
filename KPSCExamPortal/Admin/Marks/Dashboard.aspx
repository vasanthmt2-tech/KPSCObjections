<%@ Page Title="Dashboard - Marks Objection Portal" Language="C#" MasterPageFile="~/Admin.Master" AutoEventWireup="true" CodeBehind="Dashboard.aspx.cs" Inherits="KPSCExamPortal.Admin.Marks.Dashboard" %>
<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">

    <style>
        .dash-card { border: 1px solid #dee2e6; border-radius: .5rem; padding: 1rem 1.1rem; background: #fff; height: 100%; }
        .dash-label { font-size: .8rem; font-weight: 600; text-transform: uppercase; letter-spacing: .03em; color: #6c757d; }
        .dash-value { font-size: 1.9rem; font-weight: 700; line-height: 1.2; margin-top: .25rem; font-variant-numeric: tabular-nums; }
        .dash-sub { font-size: .85rem; color: #6c757d; margin-top: .2rem; }
        .dash-accent { border-top: 4px solid #0d6efd; }
        .dash-ok { border-top: 4px solid #198754; }
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
                Marks objections submitted by candidates.
            </p>

            <%-- Summary --%>
            <div class="row g-3">
                <div class="col-12 col-sm-6 col-xl-3">
                    <div class="dash-card dash-accent">
                        <div class="dash-label">Objections submitted</div>
                        <div class="dash-value"><asp:Literal ID="litSubmitted" runat="server" /></div>
                        <div class="dash-sub"><asp:Literal ID="litSubmittedSub" runat="server" /></div>
                    </div>
                </div>
                <div class="col-12 col-sm-6 col-xl-3">
                    <div class="dash-card dash-ok">
                        <div class="dash-label">With supporting document</div>
                        <div class="dash-value"><asp:Literal ID="litDocuments" runat="server" /></div>
                        <div class="dash-sub"><asp:Literal ID="litDocumentsSub" runat="server" /></div>
                    </div>
                </div>
                <div class="col-12 col-sm-6 col-xl-3">
                    <div class="dash-card dash-muted">
                        <div class="dash-label">Candidates uploaded</div>
                        <div class="dash-value"><asp:Literal ID="litCandidates" runat="server" /></div>
                        <div class="dash-sub"><asp:Literal ID="litCandidatesSub" runat="server" /></div>
                    </div>
                </div>
                <div class="col-12 col-sm-6 col-xl-3">
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
                                    <th class="num">With Document</th>
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
                                    <td class="num"><%#: Num(Eval("Documents")) %></td>
                                </tr>
                    </ItemTemplate>
                    <FooterTemplate>
                            </tbody>
                            <tfoot>
                                <tr>
                                    <td colspan="3">Total</td>
                                    <td class="num"><%#: Num(TotalSubmitted) %></td>
                                    <td></td>
                                    <td class="num"><%#: Num(TotalDocuments) %></td>
                                </tr>
                            </tfoot>
                        </table>
                    </FooterTemplate>
                </asp:Repeater>
            </div>
            <div class="form-text">
                A candidate can submit one objection per subject.
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
                                </tr>
                            </thead>
                            <tbody>
                    </HeaderTemplate>
                    <ItemTemplate>
                                <tr>
                                    <td><%#: Day(Eval("Day")) %></td>
                                    <td class="num fw-bold"><%#: Num(Eval("Objections")) %></td>
                                    <td class="num"><%#: Num(Eval("Candidates")) %></td>
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
