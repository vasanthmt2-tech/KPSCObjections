<%@ Page Title="Objections - Marks Objection Portal" Language="C#" MasterPageFile="~/Admin.Master" AutoEventWireup="true" CodeBehind="AdminObjection.aspx.cs" Inherits="KPSCExamPortal.Admin.Marks.AdminObjection" %>
<asp:Content ID="Content1" ContentPlaceHolderID="HeadContent" runat="server">
</asp:Content>
<asp:Content ID="Content2" ContentPlaceHolderID="MainContent" runat="server">
     <link href="<%: ResolveUrl("~/css/Objection.css?v=2") %>" rel="stylesheet" />
    <div class="candidate-panel">
        <div class="panel-heading">
                OBJECTION DETAILS
            </div>

            <div class="candidate-body">
                <div class="row mb-3">
                    <div class="col-md-12">

                        <asp:LinkButton ID="lnkObjectionDetails" runat="server"
                            Text="Objection Details"
                            CssClass="btn btn-primary me-2"
                            OnClick="lnkObjectionDetails_Click" />

                        <asp:LinkButton ID="lnkObjectionSummary" runat="server"
                            Text="Subject Wise Objections"
                            CssClass="btn btn-outline-primary"
                            OnClick="lnkObjectionSummary_Click" />

                    </div>
                </div>
            </div>
            <div class="candidate-body" id="divObjectionDetails" runat="server">
                <div class="row">
                    <%-- Shown 50 at a time; the full list is in the Excel download. --%>
                    <div class="d-flex flex-wrap align-items-center gap-2 mt-2">
                        <asp:Label ID="lblDetailsCount" runat="server" CssClass="fw-bold" />
                        <asp:Button ID="btnDetailsExcel" runat="server"
                            Text="Download all as Excel"
                            CssClass="btn btn-success btn-sm"
                            Visible="false"
                            OnClick="btnDetailsExcel_Click" />
                    </div>
                        <%-- Objection Details Grid --%>
                    <div class="table-responsive mt-3">
                        <asp:GridView ID="gvObjections" runat="server"
                            AutoGenerateColumns="False"
                            CssClass="table table-bordered table-striped table-hover"
                            HeaderStyle-CssClass="table-dark"
                            EmptyDataText="No objections submitted yet."
                            GridLines="None"
                            Width="100%"
                            AllowPaging="True"
                            AllowCustomPaging="True"
                            PageSize="50"
                            OnPageIndexChanging="gvObjections_PageIndexChanging">

                            <Columns>
                                <asp:BoundField DataField="ExamDesc"
                                   HeaderText="Examination" />

                                <asp:BoundField DataField="SubjectDesc"
                                   HeaderText="Subject" />

                                <asp:BoundField DataField="RegNo"
                                    HeaderText="Reg. No." />

                                <asp:BoundField DataField="VersionCode"
                                    HeaderText="Version Code" />

                                <asp:BoundField DataField="Remarks"
                                    HeaderText="Remarks" />

                                <asp:BoundField DataField="SupportingDocument"
                                    HeaderText="Document" />

                                <asp:BoundField DataField="SubmittedDate"
                                    HeaderText="Submitted Date" />

                            </Columns>
                        </asp:GridView>
                    </div>
                </div>
            </div>

            <div class="candidate-body" id="divObjectionSummary" runat="server" visible="false">
                <div class="row">
                   <%-- One row per examination and subject --%>
                    <div class="table-responsive mt-4">
                        <asp:GridView ID="gvObjectionSummary" runat="server"
                            AutoGenerateColumns="False"
                            CssClass="table table-bordered table-striped table-hover"
                            HeaderStyle-CssClass="table-dark"
                            DataKeyNames="ExamCode,SubjectCode"
                            EmptyDataText="No objection summary available."
                            GridLines="None"
                            Width="100%"
                            OnRowCommand="gvObjectionSummary_RowCommand">

                            <Columns>
                                <asp:BoundField DataField="ExamDesc"
                                   HeaderText="Examination" />

                                <asp:BoundField DataField="SubjectDesc"
                                   HeaderText="Subject" />

                                <asp:BoundField DataField="NoOfObjections"
                                    HeaderText="No. Of Objections" />

                                <asp:BoundField DataField="NoOfDocuments"
                                    HeaderText="With Document" />

                                <asp:TemplateField HeaderText="Export Excel">
                                    <ItemTemplate>
                                        <asp:LinkButton ID="lnkExcel" runat="server"
                                            Text="Download Excel"
                                            CssClass="btn btn-success btn-sm"
                                            CommandName="DownloadExcel"
                                            CommandArgument="<%# Container.DataItemIndex %>">
                                        </asp:LinkButton>
                                    </ItemTemplate>
                                </asp:TemplateField>
                                <asp:TemplateField HeaderText="Download">
                                    <ItemTemplate>
                                        <asp:LinkButton ID="lnkDownload" runat="server"
                                            Text="Download ZIP"
                                            CssClass="btn btn-primary btn-sm"
                                            CommandName="DownloadZip"
                                            Visible='<%# Convert.ToInt32(Eval("NoOfDocuments")) > 0 %>'
                                            CommandArgument="<%# Container.DataItemIndex %>">
                                        </asp:LinkButton>
                                    </ItemTemplate>
                                </asp:TemplateField>
                            </Columns>
                        </asp:GridView>
                    </div>
                </div>
            </div>
        </div>
</asp:Content>
