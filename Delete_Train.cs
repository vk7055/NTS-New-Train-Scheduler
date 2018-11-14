/*
    This module is used to delete a train from the database.
*/


using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Globalization;

namespace Train_Scheduler_2
{
    class Delete_Train
    {
        //  Connection to database
        public static string conn_String = "Data Source=(localdb)\\Projects;Initial Catalog=Train_Database_1;Integrated Security=True;MultipleActiveResultSets=True;";

        //  function to delete the train
        public Boolean is_train_deleted(String train_number)
        {
            //  Assume train with given train_number exists in the database
            Boolean is_found = true;

            Test_Case_Validator t = new Test_Case_Validator();

            is_found = t.is_valid_train(train_number);

            //  if the train exists in our database
            if (is_found == true)
            {
                SqlConnection conn = new SqlConnection(conn_String);
                conn.Open();
                if (conn.State == System.Data.ConnectionState.Open)
                {
                    string sql_query = "delete from TRAIN where TRAIN_NO = '" + train_number + "'";
                    SqlCommand cmd = new SqlCommand(sql_query, conn);
                    var temp = cmd.ExecuteNonQuery();

                    sql_query = "delete from USES where TRAIN_NO = '" + train_number + "'";
                    cmd = new SqlCommand(sql_query, conn);
                    temp = cmd.ExecuteNonQuery();

                    sql_query = "delete from OVERTAKE where TRAIN_1_CODE = '" + train_number + "' OR TRAIN_2_CODE = '" + train_number + "'";
                    cmd = new SqlCommand(sql_query, conn);
                    temp = cmd.ExecuteNonQuery();

                    sql_query = "delete from XING where TRAIN_1_CODE = '" + train_number + "' OR TRAIN_2_CODE = '" + train_number + "'";
                    cmd = new SqlCommand(sql_query, conn);
                    temp = cmd.ExecuteNonQuery();
                }
                conn.Close();
            }

            return is_found;
        }
    }
}
