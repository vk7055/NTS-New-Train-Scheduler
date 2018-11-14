/*
    This class is used to test the validity of test cases.
*/


using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Globalization;

namespace Train_Scheduler_2
{
    class Test_Case_Validator
    {
        //  Connection to database
        public static string conn_String = "Data Source=(localdb)\\Projects;Initial Catalog=Train_Database_1;Integrated Security=True;MultipleActiveResultSets=True;";

        //  check test case validity
        public Boolean is_test_case_valid(String source_station_code, String destination_station_code, String source_station_departure_time,
                                String destination_station_arrival_time, String[] intermediate_halting_stations,
                                String[] halt_duration_at_intermediate_halting_stations, char[] days_of_departure_from_source,
                                int arrival_day_num_at_destination_station, float max_speed, int train_type)
        {
            //  assume test case to be valid
            Boolean valid = true;

            //  check source station code validity
            Boolean temp = is_valid_station(source_station_code);
            valid = valid && temp;
            if (valid == false)
            {
                Console.WriteLine("\n\tInvalid Source Station Code     :   " + source_station_code);
                return valid;
            }

            //  check destination station code validity
            temp = is_valid_station(destination_station_code);
            valid = valid && temp;
            if (valid == false)
            {
                Console.WriteLine("\n\tInvalid Destination Station Code     :   " + destination_station_code);
                return valid;
            }

            //  check intermediate station code validity
            foreach (String intermediate_station_code in intermediate_halting_stations)
            {
                temp = is_valid_station(intermediate_station_code);
                valid = valid && temp;
                if (valid == false)
                {
                    Console.WriteLine("\n\tInvalid Intermediate Station Code     :   " + intermediate_station_code);
                    return valid;
                }
            }

            //  check source station departure time validity
            temp = is_valid_time_format(source_station_departure_time);
            valid = valid && temp;
            if (valid == false)
            {
                Console.WriteLine("\n\tInvalid Source Station Departure Time     :   " + source_station_departure_time);
                return valid;
            }

            //  check destination station arrival time validity
            temp = is_valid_time_format(destination_station_arrival_time);
            valid = valid && temp;
            if (valid == false)
            {
                Console.WriteLine("\n\tInvalid Destination Station Arrival Time     :   " + destination_station_arrival_time);
                return valid;
            }

            //  check intermediate station halt duration validity
            foreach (String halt_duration in halt_duration_at_intermediate_halting_stations)
            {
                temp = is_valid_time_span_format(halt_duration);
                valid = valid && temp;
                if (valid == false)
                {
                    Console.WriteLine("\n\tInvalid Intermediate Station Halt Duration     :   " + halt_duration);
                    return valid;
                }
            }

            //  check validity of days of departure from source
            temp = is_valid_weekly_schedule(days_of_departure_from_source);
            valid = valid && temp;
            if (valid == false)
            {
                Console.WriteLine("\n\tInvalid Days of Departure from Source");
                return valid;
            }

            //  check validity of arrival day number at destination station
            temp = is_valid_arrival_day_number_at_destination_station(arrival_day_num_at_destination_station);
            valid = valid && temp;
            if (valid == false)
            {
                Console.WriteLine("\n\tArrival Day Number at Destination Station should be Positive");
                return valid;
            }

            //  check validity of train type
            temp = is_valid_train_type(train_type);
            valid = valid && temp;
            if (valid == false)
            {
                Console.WriteLine("\n\tTrain Type should be lesser than 14.");
                return valid;
            }

            //  check validity of maximum speed of the train
            temp = is_valid_max_speed(max_speed);
            valid = valid && temp;
            if (valid == false)
            {
                Console.WriteLine("\n\tMaximum Speed should be between 0 and 150 Km/h");
                return valid;
            }

            //  check if the intermediate station sequence is valid
            temp = is_intermediate_halting_station_sequence_valid(source_station_code, destination_station_code, intermediate_halting_stations);
            valid = valid && temp;
            if (valid == false)
            {
                Console.WriteLine("\n\tInvalid Intermediate Station Sequence");
                return valid;
            }

            //  check if enough time is beween departure at source station and arrival
            //  at destination station considering maximum speed of train and halts
            temp = is_time_enough(source_station_code, destination_station_code, source_station_departure_time,
                                    destination_station_arrival_time, halt_duration_at_intermediate_halting_stations,
                                    arrival_day_num_at_destination_station, max_speed);
            valid = valid && temp;
            if (valid == false)
            {
                Console.WriteLine("\n\tNot Enough Time for Train to Reach to Destination");
                return valid;
            }

            return valid;
        }

        //  check station code validity
        public Boolean is_valid_station(String station_code)
        {
            //  assume station code to be valid
            Boolean valid = true;

            SqlConnection conn = new SqlConnection(conn_String);
            conn.Open();
            if (conn.State == System.Data.ConnectionState.Open)
            {
                string sql_query = "select * from STATION where STATION_CODE = '" + station_code + "'";
                SqlCommand cmd = new SqlCommand(sql_query, conn);
                SqlDataReader myreader = cmd.ExecuteReader();

                //  if station code is in invalid, myreader won't have any row
                if (myreader.HasRows == false)
                    valid = false;
            }
            conn.Close();

            return valid;
        }

        //  check train number validity
        public Boolean is_valid_train(String train_number)
        {
            //  assume station code to be valid
            Boolean valid = true;

            SqlConnection conn = new SqlConnection(conn_String);
            conn.Open();
            if (conn.State == System.Data.ConnectionState.Open)
            {
                string sql_query = "select * from TRAIN where TRAIN_NO = '" + train_number + "'";
                SqlCommand cmd = new SqlCommand(sql_query, conn);
                SqlDataReader myreader = cmd.ExecuteReader();

                //  if train number does not exist, myreader won't have any row
                if (myreader.HasRows == false)
                    valid = false;
            }
            conn.Close();

            return valid;
        }

        //  check time format validity
        public Boolean is_valid_time_format(String time)
        {
            //  assume time to be in valid format
            Boolean valid = true;

            try
            {
                DateTime temp = DateTime.ParseExact(time, "HH:mm", CultureInfo.CurrentCulture);
            }
            catch (Exception e)
            {
                valid = false;
            }

            return valid;
        }

        //  check time span format validity
        public Boolean is_valid_time_span_format(String time_span)
        {
            //  assume time span to be in valid format
            Boolean valid = true;

            try
            {
                TimeSpan halt_duration = TimeSpan.Parse(time_span);
            }
            catch (Exception e)
            {
                valid = false;
            }

            return valid;
        }

        //  check validity of weekly schedule
        public Boolean is_valid_weekly_schedule(char[] weekly_schedule)
        {
            //  assume weekly schedule to be valid
            Boolean valid = true;

            char[] week_days = { 'S', 'M', 'T', 'W', 'T', 'F', 'S' };

            for (int i = 0; i < 7; i++)
            {
                if (weekly_schedule[i] != 'N' && weekly_schedule[i] != week_days[i])
                    return false;
            }

            return valid;
        }

        //  check validity of arrival day number at destination station
        public Boolean is_valid_arrival_day_number_at_destination_station(int arrival_day_number)
        {
            Boolean valid = true;

            if (!(arrival_day_number > 0))
                return false;

            return valid;
        }

        //  check validity of train type
        public Boolean is_valid_train_type(int train_type)
        {
            Boolean valid = true;

            if (train_type > 13)
                return false;

            return valid;
        }

        //  check validity of maximum speed of the train
        public Boolean is_valid_max_speed(float max_speed)
        {
            Boolean valid = true;

            //  Maximum Speed of the train should not exceed 150 Km/h
            if (!(max_speed > 0 && max_speed <= 150))
                return false;

            return valid;
        }

        //  check if there is enough time beween departure at source station and arrival
        //  at destination station considering maximum speed of train and halts
        public Boolean is_time_enough(String source_station_code, String destination_station_code, String source_station_departure_time,
                                String destination_station_arrival_time, String[] halt_duration_at_intermediate_halting_stations,
                                int arrival_day_num_at_destination_station, float max_speed)
        {
            Boolean valid = true;

            SqlConnection conn = new SqlConnection(conn_String);
            conn.Open();
            if (conn.State == System.Data.ConnectionState.Open)
            {
                //  find the journey length of the train with given source and destination station having minimum journey length
                String sql_query = "select TOP 1 LENGTH_OF_JOURNEY from TRAIN where SOURCE_STATION_CODE = '" + source_station_code
                                            + "' AND DESTINATION_STATION_CODE = '" + destination_station_code
                                            + "' ORDER BY LENGTH_OF_JOURNEY ASC";
                SqlCommand cmd = new SqlCommand(sql_query, conn);
                double shortest_length_of_journey = (double)cmd.ExecuteScalar();

                //  find maximum duration of the journey
                TimeSpan maximum_duration_of_journey = get_time(destination_station_arrival_time) - get_time(source_station_departure_time);
                //  if journey spans over more than a day
                maximum_duration_of_journey = maximum_duration_of_journey.Add(TimeSpan.FromHours(24 * (arrival_day_num_at_destination_station - 1)));

                //  if source station departure time follows destinatiom station arrival time 
                //  in a lexicographical order
                if (String.Compare(source_station_departure_time, destination_station_arrival_time) > 0)
                {
                    maximum_duration_of_journey = get_time(destination_station_arrival_time) - get_time(source_station_departure_time);
                    //  if journey spans over more than a day
                    maximum_duration_of_journey = maximum_duration_of_journey.Add(TimeSpan.FromHours(24 * (arrival_day_num_at_destination_station - 1)));
                }

                //  find total halt duration
                TimeSpan total_halt_duration = TimeSpan.FromMinutes(0);
                foreach (var halt_span in halt_duration_at_intermediate_halting_stations)
                {
                    total_halt_duration = total_halt_duration.Add(TimeSpan.Parse(halt_span));
                }

                //  find time for which train will be mobie
                TimeSpan travel_time = TimeSpan.FromHours(shortest_length_of_journey / max_speed);

                if (travel_time + total_halt_duration > maximum_duration_of_journey)
                    valid = false;
            }
            conn.Close();

            return valid;
        }

        //  check if the intermediate station sequence is valid
        public Boolean is_intermediate_halting_station_sequence_valid(String source_station_code, String destination_station_code,
                                                                            String[] intermediate_halting_stations)
        {
            //  assume the intermediate station sequence to be valid
            Boolean valid = true;

            SqlConnection conn = new SqlConnection(conn_String);
            conn.Open();
            if (conn.State == System.Data.ConnectionState.Open)
            {
                //  find trains (train numbers) with given source and destination
                String sql_query = "select TRAIN_NO from TRAIN where SOURCE_STATION_CODE = '" + source_station_code
                                            + "' AND DESTINATION_STATION_CODE = '" + destination_station_code
                                            + "' ORDER BY LENGTH_OF_JOURNEY ASC";
                SqlCommand cmd = new SqlCommand(sql_query, conn);
                SqlDataReader myreader = cmd.ExecuteReader();

                //  get trains (train numbers) from the reader
                List<String> trains_with_same_source_destination_pair = new List<String>();
                while (myreader.Read())
                {
                    trains_with_same_source_destination_pair.Add(myreader[0].ToString());
                }

                //  assume i to be the index for intermediate halting stations
                int i = 0;
                foreach (var train_num in trains_with_same_source_destination_pair)
                {
                    i = 0;
                    String sql_query_1 = "select STATION_CODE from USES where TRAIN_NO = '" + train_num + "' ORDER BY DISTANCE_TRAVELLED ASC";
                    SqlCommand cmd_1 = new SqlCommand(sql_query_1, conn);
                    SqlDataReader myreader_1 = cmd_1.ExecuteReader();

                    while (myreader_1.Read())
                    {
                        if (intermediate_halting_stations[i].Equals(myreader_1[0].ToString()))
                            i++;

                        //  if a valid sequence is found
                        if (i == intermediate_halting_stations.Length)
                            break;
                    }

                    //  if a valid sequence is found
                    if (i == intermediate_halting_stations.Length)
                        break;
                }

                //  if a valid sequence is not found
                if (i != intermediate_halting_stations.Length)
                    valid = false;
            }
            conn.Close();
            return valid;
        }

        //  function returns time in DateTime type using argument time in string form "hh:mm"
        public DateTime get_time(String time)
        {
            return DateTime.ParseExact(time, "HH:mm", CultureInfo.CurrentCulture);
        }
    }
}