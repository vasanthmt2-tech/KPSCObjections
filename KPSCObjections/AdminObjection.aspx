<%@ Page Title="" Language="C#" MasterPageFile="~/Admin.Master" AutoEventWireup="true" CodeBehind="AdminObjection.aspx.cs" Inherits="KPSCObjections.AdminObjection" %>
<asp:Content ID="Content1" ContentPlaceHolderID="HeadContent" runat="server">
</asp:Content>
<asp:Content ID="Content2" ContentPlaceHolderID="MainContent" runat="server">
     <link href="css/Objection.css" rel="stylesheet" />
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
                            Text="Question Wise Objections"
                            CssClass="btn btn-outline-primary"
                            OnClick="lnkObjectionSummary_Click" />

                        <asp:LinkButton ID="lnkVerifyPayments" runat="server"
                            Text="Re-check Pending Payments"
                            CssClass="btn btn-outline-secondary ms-2"
                            OnClick="lnkVerifyPayments_Click" />

                        <asp:Label ID="lblVerifyResult" runat="server"
                            CssClass="d-block mt-2 text-muted" />

                    </div>
                </div>
            </div>
            <div class="candidate-body" id="divObjectionDetails" runat="server">
                <div class="row">
                        <!-- Objection Details Grid -->
                    <div class="table-responsive mt-4">
                        <asp:GridView ID="gvObjections" runat="server"
                            AutoGenerateColumns="False"
                            CssClass="table table-bordered table-striped table-hover"
                            HeaderStyle-CssClass="table-dark"
                            DataKeyNames="SubjectCode,VersionCode,QuestionNo"
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

                                <asp:BoundField DataField="SupportingDocument"
                                    HeaderText="Document" />

                                <asp:BoundField DataField="TransactionId"
                                    HeaderText="Transaction Id" />

                                <asp:BoundField DataField="SubmittedDate"
                                    HeaderText="Submitted Date" />
                                
                            </Columns>
                        </asp:GridView>
                    </div> 
                </div>
            </div>
        
            <div class="candidate-body" id="divObjectionSummary" runat="server" visible="false">
                <div class="row">
                   <!-- Objection Details Grid -->
                    <div class="table-responsive mt-4">
                        <asp:GridView ID="gvObjectionSummary" runat="server"
                            AutoGenerateColumns="False"
                            CssClass="table table-bordered table-striped table-hover"
                            HeaderStyle-CssClass="table-dark"
                            DataKeyNames="SubjectCode,QuestionNo"
                            EmptyDataText="No objection summary available."
                            GridLines="None"
                            Width="100%"
                            OnRowCommand="gvObjectionSummary_RowCommand">

                            <Columns>
                                <asp:BoundField DataField="SubjectDesc"
                                   HeaderText="Subject" />

                                <asp:BoundField DataField="SubjectCode"
                                   Visible="false" HeaderText="Subjectcode" />

                                <asp:BoundField DataField="QuestionNo"
                                    HeaderText="Master Question Number" />

                                <asp:BoundField DataField="NoOfObjections"
                                    HeaderText="No. Of Objections" />
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
