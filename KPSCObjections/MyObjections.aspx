<%@ Page Title="" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="MyObjections.aspx.cs" Inherits="KPSCObjections.MyObjections" %>
<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <link href="css/Objection.css" rel="stylesheet" />
    <div class="candidate-panel">

           
            <div class="panel-heading">
                OBJECTION DETAILS
            </div>

            <div class="candidate-body">
                <div class="row">
                   <!-- Objection Details Grid -->
                    <div class="table-responsive mt-4">
                        <asp:GridView ID="gvObjections" runat="server"
                            AutoGenerateColumns="False"
                            CssClass="table table-bordered table-striped table-hover"
                            HeaderStyle-CssClass="table-dark"
                            DataKeyNames="PaperCode,VersionCode,QuestionNo"
                            EmptyDataText="No objections submitted yet. Unpaid objections are listed under File Objection."
                            GridLines="None"
                            Width="100%">

                            <Columns>
                                <asp:BoundField DataField="SubjectCode"
                                    HeaderText="Subject" />

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
                                            Text='<%#: Eval("SupportingDocument") %>'
                                            NavigateUrl='<%# "~/ObjectionDocument.ashx?paper=" + HttpUtility.UrlEncode(Convert.ToString(Eval("PaperCode"))) + "&q=" + Eval("QuestionNo") %>'>
                                        </asp:HyperLink>
                                    </ItemTemplate>
                                </asp:TemplateField>

                                <asp:BoundField DataField="TransactionId"
                                    HeaderText="Transaction Id" />

                                <asp:BoundField DataField="SubmittedDate"
                                    HeaderText="Payment Date" />

                                <asp:BoundField DataField="Status"
                                    HeaderText="Status" />

                            </Columns>
                        </asp:GridView>
                    </div>                    
                </div>
            </div>
        </div>
</asp:Content>
