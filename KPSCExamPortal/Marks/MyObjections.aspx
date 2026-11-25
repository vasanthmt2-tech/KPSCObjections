<%@ Page Title="My Objections - Marks Objection Portal" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="MyObjections.aspx.cs" Inherits="KPSCExamPortal.Marks.MyObjections" %>
<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <link href="<%: ResolveUrl("~/css/Objection.css?v=2") %>" rel="stylesheet" />
    <div class="candidate-panel">

            <div class="panel-heading">
                OBJECTION DETAILS
            </div>

            <div class="candidate-body">
                <div class="row">
                   <%-- Submitted marks objections --%>
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
                                            NavigateUrl='<%# KPSCExamPortal.Security.DocumentReferences.MarksUrl(Session, Eval("PaperCode")) %>'>
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
