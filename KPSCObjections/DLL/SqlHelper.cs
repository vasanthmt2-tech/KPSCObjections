using System;
using System.Data;
using System.Data.SqlClient;
using System.Data.OleDb;
using System.Configuration;
using System.EnterpriseServices;
using System.Collections.Generic;

namespace KPSCObjections.DLL
{
    public class SqlHelper
    {
        public SqlHelper()
        {
            strConnectionString = ConfigurationManager.ConnectionStrings["Objection_ConnectionString"].ConnectionString;
        }
        private string strConnectionString = "";


        public DataTable ExecuteDataTable(string query, params SqlParameter[] parameters)
        {
            SqlConnection cnn = new SqlConnection(strConnectionString);
            SqlCommand cmd = new SqlCommand(query, cnn);
            SqlDataAdapter da = new SqlDataAdapter();
            DataTable dt = new DataTable();
            if (query.ToLower().Trim().StartsWith("select"))
            {
                cmd.CommandType = CommandType.Text;
            }
            else
            {
                cmd.CommandType = CommandType.StoredProcedure;
            }
            for (int i = 0; i <= parameters.Length - 1; i++)
            {
                cmd.Parameters.Add(parameters[i]);
            }
            try
            {
                cmd.CommandTimeout = 0;
                da.SelectCommand = cmd;
                da.Fill(dt);
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                da.Dispose();
            }
            return dt;
        }
        public DataTable ExecuteDataTable(string Query, string ParamNames, string ParamValues)
        {
            // Line breaks become spaces, so a multi-line query does not have
            // its lines glued together ("...o.RegNoFROM dbo...").
            Query = Query.Replace("\r", " ");
            Query = Query.Replace("\n", " ");
            Query = Query.Replace("\t", " ");
            DataTable dt = new DataTable();
            ParamValues = "," + ParamValues;
            SqlConnection cnn = new SqlConnection(strConnectionString);
            SqlCommand cmd = new SqlCommand(Query, cnn);
            SqlDataAdapter da = new SqlDataAdapter();
            string[] aStrtoSplit = new string[] { ",@" };
            string[] aParamNames = ParamNames.Replace(" ", "").Split(',');
            string[] aParamValues = ParamValues.Split(aStrtoSplit, StringSplitOptions.RemoveEmptyEntries);
            if (aParamNames.Length != aParamValues.Length)
            {
                return (null);
            }
            if (Query.ToLower().Trim().StartsWith("select"))
            {
                cmd.CommandType = CommandType.Text;
            }
            else if (Query.ToLower().Trim().StartsWith("insert"))
            {
                cmd.CommandType = CommandType.Text;
            }
            else if (Query.ToLower().Trim().StartsWith("update"))
            {
                cmd.CommandType = CommandType.Text;
            }
            else if (Query.ToLower().Trim().StartsWith("delete"))
            {
                cmd.CommandType = CommandType.Text;
            }
            else
            {
                cmd.CommandType = CommandType.StoredProcedure;
            }
            if (aParamNames[0] != "")
            {
                for (int i = 0; i < aParamNames.Length; i++)
                {
                    if (aParamNames[i].Trim().Substring(0, 1) == "@")
                    {
                        cmd.Parameters.Add(new SqlParameter(aParamNames[i], aParamValues[i]));
                    }
                }
            }
            try
            {
                cmd.CommandTimeout = 0;
                da.SelectCommand = cmd;
                da.Fill(dt);
            }
            catch (Exception ex)
            {
                // Utilities.Report_Error(ex);
            }
            finally
            {
                da.Dispose();
            }
            return (dt);
        }

        public SqlDataReader ExecuteDataReader(string query, string ParamNames, string ParamValues)
        {
            query = query.Replace("\r", " ");
            query = query.Replace("\n", " ");
            query = query.Replace("\t", " ");
            query = query.TrimStart();
            ParamValues = "," + ParamValues;
            SqlConnection cnn = new SqlConnection(strConnectionString);
            SqlCommand cmd = new SqlCommand(query, cnn);
            SqlDataReader dr;
            string[] aStrtoSplit = new string[] { ",@" };
            string[] aParamNames = ParamNames.Replace(" ", "").Split(',');
            string[] aParamValues = ParamValues.Split(aStrtoSplit, StringSplitOptions.RemoveEmptyEntries);
            if (aParamNames.Length != aParamValues.Length)
            {
                return (null);
            }
            if (query.StartsWith("SELECT") | query.StartsWith("select") | query.StartsWith("Select") | query.StartsWith(" SELECT") | query.StartsWith(" select") | query.StartsWith(" Select"))
            {
                cmd.CommandType = CommandType.Text;
            }
            else
            {
                cmd.CommandType = CommandType.StoredProcedure;
            }
            if (aParamNames[0] != "")
            {
                for (int i = 0; i < aParamNames.Length; i++)
                {
                    if (aParamNames[i].Trim().Substring(0, 1) == "@")
                    {
                        cmd.Parameters.Add(new SqlParameter(aParamNames[i], aParamValues[i]));
                    }
                }
            }
            //for (int i = 0; i <= parameters.Length - 1; i++)
            //{
            //    cmd.Parameters.Add(parameters[i]);
            //}
            try
            {
                cnn.Open();
                cmd.CommandTimeout = 0;
                dr = cmd.ExecuteReader(CommandBehavior.CloseConnection);
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                if (cnn.State == ConnectionState.Open)
                {
                    //    cnn.Close();
                }
            }
            return dr;
        }

        public SqlDataReader ExecuteDataReader(string query, params SqlParameter[] parameters)
        {
            SqlConnection cnn = new SqlConnection(strConnectionString);
            SqlCommand cmd = new SqlCommand(query, cnn);
            SqlDataReader dr;
            if (query.StartsWith("SELECT") | query.StartsWith("select") | query.StartsWith("Select") | query.StartsWith(" SELECT") | query.StartsWith(" select") | query.StartsWith(" Select"))
            {
                cmd.CommandType = CommandType.Text;
            }
            else
            {
                cmd.CommandType = CommandType.StoredProcedure;
            }
            for (int i = 0; i <= parameters.Length - 1; i++)
            {
                cmd.Parameters.Add(parameters[i]);
            }
            try
            {
                cnn.Open();
                cmd.CommandTimeout = 0;
                dr = cmd.ExecuteReader(CommandBehavior.CloseConnection);
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                if (cnn.State == ConnectionState.Open)
                {
                    //    cnn.Close();
                }
            }
            return dr;
        }

        public int ExecuteNonQuery(string query, params SqlParameter[] parameters)
        {
            using (SqlConnection cnn = new SqlConnection(strConnectionString))
            using (SqlCommand cmd = new SqlCommand(query, cnn))
            {
                cmd.CommandType = CommandType.Text;
                cmd.CommandTimeout = 60;

                if (parameters != null)
                    cmd.Parameters.AddRange(parameters);

                cnn.Open();
                return cmd.ExecuteNonQuery();
            }
        }

        public object ExecuteScalar(string query, params SqlParameter[] parameters)
        {
            using (SqlConnection cnn = new SqlConnection(strConnectionString))
            using (SqlCommand cmd = new SqlCommand(query, cnn))
            {
                cmd.CommandType = CommandType.Text;
                cmd.CommandTimeout = 60;

                if (parameters != null)
                    cmd.Parameters.AddRange(parameters);

                cnn.Open();
                return cmd.ExecuteScalar();
            }
        }
    }    
}