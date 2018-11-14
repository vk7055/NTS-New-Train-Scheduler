/*
    This file parses the text file containing test cases.
    It is called by Crawler.cs and passes the parsed 
    contents to Scheduler.cs
 */

using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Train_Scheduler_2
{
    class Test_File_Parser
    {
        //  read test file as a single string
        string text = File.ReadAllText(@"D:\\Documents\\IIT Patna\\ACADEMIC\\SEMESTER VII\\BTP\\Test Case.txt", Encoding.UTF8);

        //  returns source station code
        public string find_source_station_code()
        {
            var source_station_code_pattern = @"Source Station Code.*:-[\t][A-Z]*";
            Regex source_station_code_rgx = new Regex(source_station_code_pattern, RegexOptions.IgnoreCase);
            MatchCollection source_station_code_matches = source_station_code_rgx.Matches(text);

            string temp = source_station_code_matches[0].Value;
            Console.WriteLine("\n\t" + temp);

            int i = 0;
            while (temp[temp.Length - 1 - i] != '\t')
            {
                i++;
            }

            string source_station_code = temp.Substring(temp.Length - i);
            Console.WriteLine("\n\t" + source_station_code + "\t" + source_station_code.Length);

            return source_station_code;
        }

        //  returns destination station code
        public string find_destination_station_code()
        {
            var destination_station_code_pattern = @"Destination Station Code.*:-[\t][A-Z]*";
            Regex destination_station_code_rgx = new Regex(destination_station_code_pattern, RegexOptions.IgnoreCase);
            MatchCollection destination_station_code_matches = destination_station_code_rgx.Matches(text);

            string temp = destination_station_code_matches[0].Value;
            Console.WriteLine("\n\t" + temp);

            int i = 0;
            while (temp[temp.Length - 1 - i] != '\t')
            {
                i++;
            }

            string destination_station_code = temp.Substring(temp.Length - i);
            Console.WriteLine("\n\t" + destination_station_code + "\t" + destination_station_code.Length);

            return destination_station_code;
        }

        //  returns departure time at source station
        public string find_source_station_departure_time()
        {
            var source_station_departure_time_pattern = @"Source Station Departure Time.*:-[\t][0-9]+[:][0-9]+";
            Regex source_station_departure_time_rgx = new Regex(source_station_departure_time_pattern, RegexOptions.IgnoreCase);
            MatchCollection source_station_departure_time_matches = source_station_departure_time_rgx.Matches(text);

            string temp = source_station_departure_time_matches[0].Value;
            Console.WriteLine("\n\t" + temp);

            int i = 0;
            while (temp[temp.Length - 1 - i] != '\t')
            {
                i++;
            }

            string source_station_departure_time = temp.Substring(temp.Length - i);
            Console.WriteLine("\n\t" + source_station_departure_time + "\t" + source_station_departure_time.Length);

            return source_station_departure_time;
        }

        //  returns arrival time at destination station
        public string find_destination_station_arrival_time()
        {
            var destination_station_arrival_time_pattern = @"Destination Station Arrival Time.*:-[\t][0-9]+[:][0-9]+";
            Regex destination_station_arrival_time_rgx = new Regex(destination_station_arrival_time_pattern, RegexOptions.IgnoreCase);
            MatchCollection destination_station_arrival_time_matches = destination_station_arrival_time_rgx.Matches(text);

            string temp = destination_station_arrival_time_matches[0].Value;
            Console.WriteLine("\n\t" + temp);

            int i = 0;
            while (temp[temp.Length - 1 - i] != '\t')
            {
                i++;
            }

            string destination_station_arrival_time = temp.Substring(temp.Length - i);
            Console.WriteLine("\n\t" + destination_station_arrival_time + "\t" + destination_station_arrival_time.Length);

            return destination_station_arrival_time;
        }

        //  returns arrival day number at destination station
        public int find_arrival_day_num_at_destination_station()
        {
            var arrival_day_num_at_destination_station_pattern = @"Day Number of Arrival at Destination Station.*:-[\t][0-9]";
            Regex arrival_day_num_at_destination_station_rgx = new Regex(arrival_day_num_at_destination_station_pattern, RegexOptions.IgnoreCase);
            MatchCollection arrival_day_num_at_destination_station_matches = arrival_day_num_at_destination_station_rgx.Matches(text);

            string temp = arrival_day_num_at_destination_station_matches[0].Value;
            Console.WriteLine("\n\t" + temp);

            int i = 0;
            while (temp[temp.Length - 1 - i] != '\t')
            {
                i++;
            }

            string arrival_day_num_at_destination_station = temp.Substring(temp.Length - i);
            Console.WriteLine("\n\t" + arrival_day_num_at_destination_station + "\t" + arrival_day_num_at_destination_station.Length);

            return Int32.Parse(arrival_day_num_at_destination_station);
        }

        //  returns maximum speed of train
        public float find_max_speed_of_train()
        {
            var max_speed_of_train_pattern = @"Maximum Speed of the Train.*:-[\t][0-9]+";
            Regex max_speed_of_train_rgx = new Regex(max_speed_of_train_pattern, RegexOptions.IgnoreCase);
            MatchCollection max_speed_of_train_matches = max_speed_of_train_rgx.Matches(text);

            string temp = max_speed_of_train_matches[0].Value;
            Console.WriteLine("\n\t" + temp);

            int i = 0;
            while (temp[temp.Length - 1 - i] != '\t')
            {
                i++;
            }

            string max_speed_of_train = temp.Substring(temp.Length - i);
            Console.WriteLine("\n\t" + max_speed_of_train + "\t" + max_speed_of_train.Length);

            return float.Parse(max_speed_of_train, CultureInfo.InvariantCulture.NumberFormat);
        }

        //  returns days of origin from source station
        public char[] find_days_of_origin_from_source()
        {

            var days_of_origin_text_pattern = @"[{].*[}]";
            Regex days_of_origin_text_rgx = new Regex(days_of_origin_text_pattern, RegexOptions.IgnoreCase);
            MatchCollection days_of_origin_text_matches = days_of_origin_text_rgx.Matches(text);

            string temp = days_of_origin_text_matches[0].Value;
            Console.WriteLine("\n\n\t" + temp + "\t" + temp.Length);

            var days_of_origin_pattern = @"[A-Z]";
            Regex days_of_origin_rgx = new Regex(days_of_origin_pattern, RegexOptions.IgnoreCase);
            MatchCollection days_of_origin_matches = days_of_origin_rgx.Matches(temp);

            char[] days_of_origin = { 'N', 'N', 'N', 'N', 'N', 'N', 'N' };

            Console.Write("\n\t");
            for (int i = 0; i < 7; i++)
            {
                days_of_origin[i] = (days_of_origin_matches[i].Value)[0];
                Console.Write(days_of_origin[i] + " ");
            }

            return days_of_origin;
        }

        //  returns station codes of intermediate halting stations
        public String[] find_intermediate_halting_station_codes()
        {

            var list_of_stations_text_pattern = @"[{].*[}]";
            Regex list_of_stations_text_rgx = new Regex(list_of_stations_text_pattern, RegexOptions.IgnoreCase);
            MatchCollection list_of_stations_text_matches = list_of_stations_text_rgx.Matches(text);

            string temp = list_of_stations_text_matches[1].Value;
            Console.WriteLine("\n\n\t" + temp + "\t" + temp.Length);

            var list_of_stations_pattern = @""".*?""";
            Regex list_of_stations_rgx = new Regex(list_of_stations_pattern, RegexOptions.IgnoreCase);
            MatchCollection list_of_stations_matches = list_of_stations_rgx.Matches(temp);

            String[] list_of_stations = new String[list_of_stations_matches.Count];

            for (int i = 0; i < list_of_stations_matches.Count; i++)
            {
                list_of_stations[i] = list_of_stations_matches[i].Value;
                list_of_stations[i] = list_of_stations[i].Substring(1, list_of_stations[i].Length - 2);
                Console.WriteLine("\n\t" + list_of_stations[i]);
            }

            return list_of_stations;
        }

        //  returns halt duration at intermediate halting stations
        public String[] find_halt_duration_at_intermediate_halting_stations()
        {

            var halt_duration_at_intermediate_halting_stations_text_pattern = @"[{].*[}]";
            Regex halt_duration_at_intermediate_halting_stations_text_rgx = new Regex(halt_duration_at_intermediate_halting_stations_text_pattern, RegexOptions.IgnoreCase);
            MatchCollection halt_duration_at_intermediate_halting_stations_text_matches = halt_duration_at_intermediate_halting_stations_text_rgx.Matches(text);

            string temp = halt_duration_at_intermediate_halting_stations_text_matches[2].Value;
            Console.WriteLine("\n\n\t" + temp + "\t" + temp.Length);

            var halt_duration_at_intermediate_halting_stations_pattern = @""".*?""";
            Regex halt_duration_at_intermediate_halting_stations_rgx = new Regex(halt_duration_at_intermediate_halting_stations_pattern, RegexOptions.IgnoreCase);
            MatchCollection halt_duration_at_intermediate_halting_stations_matches = halt_duration_at_intermediate_halting_stations_rgx.Matches(temp);

            String[] halt_duration_at_intermediate_halting_stations = new String[halt_duration_at_intermediate_halting_stations_matches.Count];

            for (int i = 0; i < halt_duration_at_intermediate_halting_stations_matches.Count; i++)
            {
                halt_duration_at_intermediate_halting_stations[i] = halt_duration_at_intermediate_halting_stations_matches[i].Value;
                halt_duration_at_intermediate_halting_stations[i] = halt_duration_at_intermediate_halting_stations[i].Substring
                                                                           (1, halt_duration_at_intermediate_halting_stations[i].Length - 2);
                Console.WriteLine("\n\t" + halt_duration_at_intermediate_halting_stations[i]);
            }

            return halt_duration_at_intermediate_halting_stations;
        }

        //  returns train type of the train
        public int find_train_type()
        {
            var train_type_pattern = @"Train Type.*:-[\t][0-9]+";
            Regex train_type_rgx = new Regex(train_type_pattern, RegexOptions.IgnoreCase);
            MatchCollection train_type_matches = train_type_rgx.Matches(text);

            string temp = train_type_matches[0].Value;
            Console.WriteLine("\n\t" + temp);

            int i = 0;
            while (temp[temp.Length - 1 - i] != '\t')
            {
                i++;
            }

            string train_type = temp.Substring(temp.Length - i);
            Console.WriteLine("\n\t" + train_type + "\t" + train_type.Length);

            return Int32.Parse(train_type);
        }
    }
}
