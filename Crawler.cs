/*

    This class is used to crawl the website https://indiarailinfo.com/.
    One just needs to pass the url of a train time table and various
    details would be populated in several tables across our database.

*/

using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using HtmlAgilityPack;
using System.Linq;
using OpenQA.Selenium.Chrome;
using System.Threading;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Data.SqlClient;

namespace Train_Scheduler_2
{
    class Crawler
    {

        //Connection to database
        public static string conn_String = "Data Source=(localdb)\\Projects;Initial Catalog=Train_Database_1;Integrated Security=True";

        static void Main(string[] args)
        {
            int ch;

            Console.WriteLine("\n\t1\t:\tAdd a new Train");
            Console.WriteLine("\n\t2\t:\tPlan Schedule for a Train");
            Console.WriteLine("\n\t1\t:\tDelete a Train");
            Console.Write("\n\n\n\t\tEnter Your Choice (1, 2 or 3)  :   ");

            ch = Convert.ToInt32(Console.ReadLine());

            switch (ch)
            {
                case 1: //  function call for crawling in order to gather details for a train
                        StartCrawler();
                        break;

                case 2: //  find route for a new train

                        Test_File_Parser parser = new Test_File_Parser();

                        //  fetch details of the train
                        String source_station_code = parser.find_source_station_code();
                        String destination_station_code = parser.find_destination_station_code();
                        String source_station_departure_time = parser.find_source_station_departure_time();
                        String destination_station_arrival_time = parser.find_destination_station_arrival_time();
                        String[] intermediate_halting_stations_code = parser.find_intermediate_halting_station_codes();
                        String[] halt_duration_at_intermediate_halting_stations = parser.find_halt_duration_at_intermediate_halting_stations();
                        char[] days_of_origin = parser.find_days_of_origin_from_source();
                        int arrival_day_num_at_destination_station = parser.find_arrival_day_num_at_destination_station();
                        float max_speed = parser.find_max_speed_of_train();
                        int train_type = parser.find_train_type();

                        Test_Case_Validator validator = new Test_Case_Validator();

                        Boolean is_valid = validator.is_test_case_valid(source_station_code, destination_station_code, source_station_departure_time,
                                                                destination_station_arrival_time, intermediate_halting_stations_code,
                                                                halt_duration_at_intermediate_halting_stations, days_of_origin,
                                                                arrival_day_num_at_destination_station, max_speed, train_type);

                        if (is_valid == false)
                        {
                            Console.WriteLine("\n\tInvalid Test Case");
                            break;
                        }

                        Scheduler a = new Scheduler();

                        //  function call in order to find route for a new train
                        List<train> possible_trains = a.Schedule(source_station_code, destination_station_code, source_station_departure_time,
                                                                    destination_station_arrival_time, intermediate_halting_stations_code,
                                                                    halt_duration_at_intermediate_halting_stations, days_of_origin,
                                                                    arrival_day_num_at_destination_station, max_speed, train_type);
                        break;
                case 3: //  Function call to delete a train from the database.
                        //  This function can be used to delete any 
                        //  redundant train from the database.

                        Delete_Train d = new Delete_Train();

                        //  train no. of the train to be deleted
                        String train_number = "12261";

                        //  Return value is false if train does not exist in the database.
                        //  Return value is true if train exists in the database and all
                        //  details of train are removed from the database.
                        Boolean temp = d.is_train_deleted(train_number);

                        if (temp == false)
                            Console.WriteLine("\n\tTrain " + train_number + " not found");
                        else
                            Console.WriteLine("\n\tTrain " + train_number + " deleted");
                            
                        break;
            }
        }

        //Crawler to gather details for a train
        public static async Task StartCrawler()
        {
            //Open Browser with the Required URL
            ChromeDriver driver = new ChromeDriver();
            driver.Manage().Window.Maximize();
            driver.Navigate().GoToUrl("http://indiarailinfo.com/train/-train-mumbai-cst-howrah-ac-duronto-express-12261/7555/1620/1");

            //Click on all of the "Elements" representing Intermediate Stations to get the intermediate stations  
            var intermediate_stations = driver.FindElementsByClassName("imgDotted");

            for (int j = 0; j < intermediate_stations.Count(); j++)
            {
                try
                {
                    intermediate_stations.ElementAt(j).Click();
                }
                catch (Exception e)
                {
                    intermediate_stations = driver.FindElementsByClassName("imgDotted");
                    intermediate_stations.ElementAt(j).Click();
                }
                Thread.Sleep(1000);
            }
            //driver.Close();

            //Pause the program so that the Page Source can change completely to capture the newly loaded intermediate stations
            int milliseconds = 5000;
            Thread.Sleep(milliseconds);

            //Load the Html Document
            var htmlDocument = new HtmlDocument();
            htmlDocument.LoadHtml(driver.PageSource);

            //get div having "listingcapsulehalf" as class
            var divs_listingcapsulehalf = htmlDocument.DocumentNode.Descendants("div").Where(node => node.GetAttributeValue("class", "")
                        .Equals("listingcapsulehalf")).ToList();

            //divs[1] has a table that associates train with stations

            //Stations at which train halts has class "darkrow" 
            var halting_stations = divs_listingcapsulehalf[1].Descendants("tr").Where(node => node.GetAttributeValue("class", "")
                        .Equals("darkrow")).ToList();

            //Stations at which train does not halt has class name that contains substring "substn" 
            var non_halting_stations = divs_listingcapsulehalf[1].Descendants("tr").Where(node => node.GetAttributeValue("class", "")
                        .Contains("substn")).ToList();

            //Table rows that tells number of intermediate stations has class name that contains substring "imgDotted" 
            var num_intermediate_stations = divs_listingcapsulehalf[1].Descendants("tr").Where(node => node.GetAttributeValue("class", "")
                        .Contains("imgDotted")).ToList();

            //Check if all intermediate stations were clicked properly
            var class_substn_count = get_class_substn_count(num_intermediate_stations);
            if (class_substn_count != non_halting_stations.Count)
            {
                Console.WriteLine("\n\n\t\tUnable to click all intermedite station elements.\n\n");
            }
            else
            {
                StreamWriter sw = new StreamWriter("D:\\Documents\\IIT Patna\\ACADEMIC\\SEMESTER VII\\BTP\\schedule_test.txt");

                //div having "topcapsule" as class. It contains train number.
                var divs_topcapsule = htmlDocument.DocumentNode.Descendants("div").Where(node => node.GetAttributeValue("class", "")
                            .Equals("topcapsule")).ToList();

                //Train number
                var train_no = (divs_topcapsule[0].Descendants("h1").ToList())[0].InnerText.Substring(0, 5);

                //function call to fetch database table "TRAIN"
                get_train_table(sw, divs_topcapsule, halting_stations, divs_listingcapsulehalf);

                //function call to fetch details at stations where the train halts
                Console.WriteLine("\n\n\tHalting Stations\n");
                sw.WriteLine("\n\n\tHalting Stations\n");
                get_halting_stations_details(sw, train_no, halting_stations);

                //function call to fetch Details at stations where the train does not halt
                Console.WriteLine("\n\n\tNon Halting Stations\n");
                sw.WriteLine("\n\n\tNon Halting Stations\n");
                get_non_halting_stations_details(sw, train_no, non_halting_stations);

                //function call to fetch database table "XING"
                Console.WriteLine("\n\n\tXing\n");
                sw.WriteLine("\n\n\tXing\n");
                get_xings(sw, train_no, halting_stations, non_halting_stations);

                //function call to fetch database table "OVERTAKE" when our train overtrakes
                Console.WriteLine("\n\n\tOvertakes\n");
                sw.WriteLine("\n\n\tOvertakes\n");
                get_overtake(sw, train_no, halting_stations, non_halting_stations);

                //function call to fetch database table "OVERTAKE" when our train is overtaken
                Console.WriteLine("\n\n\tOvertaken\n");
                sw.WriteLine("\n\n\tOvertaken\n");
                get_overtaken(sw, train_no, halting_stations, non_halting_stations);

                //function call to fetch database table "NEIGHBOUR"
                Console.WriteLine("\n\n\tNeighbour\n");
                sw.WriteLine("\n\n\tNeighbour\n");
                get_neighbour(sw, halting_stations, non_halting_stations, num_intermediate_stations);

                //Complete File Writing
                sw.Flush();
            }
            Console.WriteLine("\n\t\t class_substn_count - non_halting_stations.Count = " + (class_substn_count - non_halting_stations.Count) + "\n\n");
        }

        //function to fetch track type
        public static int get_track_type(List<HtmlNode> tds)
        {
            /*
                    1   -   Single 
                    2   -   Double
                    3   -   Triple
                    4   -   Quadruple
            */
            var track_type_text = tds[1].OuterHtml;
            var track_type_pattern = @"title1=.*><span";
            Regex track_type_rgx = new Regex(track_type_pattern, RegexOptions.IgnoreCase);
            MatchCollection track_type_matches = track_type_rgx.Matches(track_type_text);
            track_type_text = (track_type_matches[0].Value).Substring(8);
            track_type_text = track_type_text.Substring(0, track_type_text.Length - 7);

            if (track_type_text.Contains("oubl"))
                return 2;
            else if (track_type_text.Contains("ripl"))
                return 3;
            else if (track_type_text.Contains("uadr"))
                return 4;
            else
                return 1;
        }

        //function returns the no. of elements that have class "substn"
        public static int get_class_substn_count(List<HtmlNode> intermediate_stations)
        {
            int class_substn_count = 0;
            foreach (var row in intermediate_stations)
            {
                var tds = row.Descendants("td").ToList();
                var num_intermediate_stations_text = tds[3].InnerText;

                var num_intermediate_stations_pattern = @"[0-9]+";
                Regex num_intermediate_stations_rgx = new Regex(num_intermediate_stations_pattern, RegexOptions.IgnoreCase);
                MatchCollection num_intermediate_stations_matches = num_intermediate_stations_rgx.Matches(num_intermediate_stations_text);
                var num_intermediate_stations = int.Parse(num_intermediate_stations_matches[0].Value);

                if (num_intermediate_stations == 0)
                    class_substn_count += 1;
                else
                    class_substn_count += num_intermediate_stations + 2;
            }
            return class_substn_count;
        }

        //function to fetch database table "NEIGHBOUR"
        public static void get_neighbour(StreamWriter sw, List<HtmlNode> halting_stations, List<HtmlNode> non_halting_stations, List<HtmlNode> intermediate_stations)
        {
            var num_substn_class_encountered = 0;

            //Get halting Stations and their neighbours
            Console.WriteLine("\n\n\tHalting Stations\n");
            sw.WriteLine("\n\n\tHalting Stations\n");

            for (int halting_station_num = 0; halting_station_num < halting_stations.Count; halting_station_num++)
            {
                var tds = halting_stations[halting_station_num].Descendants("td").ToList();

                if (halting_station_num == 0)           //First Neighbour pair if there exists an intermediate station after first station
                {
                    var tds_intermediate_stations = intermediate_stations[0].Descendants("td").ToList();    //First Intermediate Station Row
                    var num_intermediate_stations_text = tds_intermediate_stations[3].InnerText;

                    var num_intermediate_stations_pattern = @"[0-9]+";
                    Regex num_intermediate_stations_rgx = new Regex(num_intermediate_stations_pattern, RegexOptions.IgnoreCase);
                    MatchCollection num_intermediate_stations_matches = num_intermediate_stations_rgx.Matches(num_intermediate_stations_text);
                    var num_intermediate_stations = int.Parse(num_intermediate_stations_matches[0].Value);

                    if (num_intermediate_stations > 0)       //if there exists some intermediate station between first two halting stations
                    {
                        var first_non_halting_station_tds = non_halting_stations[1].Descendants("td").ToList();

                        var distance_from_first_non_halting_neighbour = float.Parse(first_non_halting_station_tds[13].InnerText, CultureInfo.InvariantCulture)
                                                                - float.Parse(tds[13].InnerText, CultureInfo.InvariantCulture);
                        Console.WriteLine("\t" + first_non_halting_station_tds[0].InnerText + "\t" + get_track_type(first_non_halting_station_tds)
                                            + "\t" + first_non_halting_station_tds[2].InnerText + "\t" + tds[2].InnerText
                                            + "\t" + first_non_halting_station_tds[13].InnerText + "\t" + distance_from_first_non_halting_neighbour);
                        sw.WriteLine("\t" + first_non_halting_station_tds[0].InnerText + "\t" + get_track_type(first_non_halting_station_tds)
                                            + "\t" + first_non_halting_station_tds[2].InnerText + "\t" + tds[2].InnerText
                                            + "\t" + first_non_halting_station_tds[13].InnerText + "\t" + distance_from_first_non_halting_neighbour);

                        //function to insert rows in NEIGHBOUR table. Inserted rows have lower lexical station first.
                        fill_neighbour_table(first_non_halting_station_tds[2].InnerText, tds[2].InnerText,
                                                distance_from_first_non_halting_neighbour, get_track_type(first_non_halting_station_tds));
                    }
                }
                else if (halting_station_num > 0 && halting_station_num < halting_stations.Count - 1)   //Intermediate Halting Stations
                {
                    var tds_intermediate_stations = intermediate_stations[halting_station_num - 1].Descendants("td").ToList();
                    var num_intermediate_stations_text = tds_intermediate_stations[3].InnerText;

                    var num_intermediate_stations_pattern = @"[0-9]+";
                    Regex num_intermediate_stations_rgx = new Regex(num_intermediate_stations_pattern, RegexOptions.IgnoreCase);
                    MatchCollection num_intermediate_stations_matches = num_intermediate_stations_rgx.Matches(num_intermediate_stations_text);
                    var num_intermediate_stations = int.Parse(num_intermediate_stations_matches[0].Value);

                    if (num_intermediate_stations > 0)       //if there exists some intermediate station before the halting station
                    {
                        num_substn_class_encountered += num_intermediate_stations + 2;

                        var last_non_halting_station_tds = non_halting_stations[num_substn_class_encountered - 2].Descendants("td").ToList();

                        var distance_from_last_non_halting_neighbour = float.Parse(tds[13].InnerText, CultureInfo.InvariantCulture)
                                                            - float.Parse(last_non_halting_station_tds[13].InnerText, CultureInfo.InvariantCulture);
                        Console.WriteLine("\t" + tds[0].InnerText + "\t" + get_track_type(tds)
                                        + "\t" + tds[2].InnerText + "\t" + last_non_halting_station_tds[2].InnerText
                                        + "\t" + tds[13].InnerText + "\t" + distance_from_last_non_halting_neighbour);
                        sw.WriteLine("\t" + tds[0].InnerText + "\t" + get_track_type(tds)
                                            + "\t" + tds[2].InnerText + "\t" + last_non_halting_station_tds[2].InnerText
                                            + "\t" + tds[13].InnerText + "\t" + distance_from_last_non_halting_neighbour);

                        //function to insert rows in NEIGHBOUR table. Inserted rows have lower lexical station first.
                        fill_neighbour_table(last_non_halting_station_tds[2].InnerText, tds[2].InnerText,
                                                distance_from_last_non_halting_neighbour, get_track_type(tds));
                    }
                    else                                //if there does not exist any intermediate station before the halting station
                    {
                        num_substn_class_encountered += num_intermediate_stations + 1;

                        var last_halting_station_tds = halting_stations[halting_station_num - 1].Descendants("td").ToList();
                        var distance_from_last_halting_neighbour = float.Parse(tds[13].InnerText, CultureInfo.InvariantCulture)
                                                            - float.Parse(last_halting_station_tds[13].InnerText, CultureInfo.InvariantCulture);

                        Console.WriteLine("\t" + tds[0].InnerText + "\t" + get_track_type(tds)
                                        + "\t" + tds[2].InnerText + "\t" + last_halting_station_tds[2].InnerText
                                        + "\t" + tds[13].InnerText + "\t" + distance_from_last_halting_neighbour);
                        sw.WriteLine("\t" + tds[0].InnerText + "\t" + get_track_type(tds)
                                        + "\t" + tds[2].InnerText + "\t" + last_halting_station_tds[2].InnerText
                                        + "\t" + tds[13].InnerText + "\t" + distance_from_last_halting_neighbour);

                        //function to insert rows in NEIGHBOUR table. Inserted rows have lower lexical station first.
                        fill_neighbour_table(last_halting_station_tds[2].InnerText, tds[2].InnerText,
                                                distance_from_last_halting_neighbour, get_track_type(tds));
                    }

                    tds_intermediate_stations = intermediate_stations[halting_station_num].Descendants("td").ToList();
                    num_intermediate_stations_text = tds_intermediate_stations[3].InnerText;

                    num_intermediate_stations_matches = num_intermediate_stations_rgx.Matches(num_intermediate_stations_text);
                    num_intermediate_stations = int.Parse(num_intermediate_stations_matches[0].Value);

                    if (num_intermediate_stations > 0)       //if there exists some intermediate station after the halting station
                    {
                        var next_non_halting_station_tds = non_halting_stations[num_substn_class_encountered + 1].Descendants("td").ToList();
                        var distance_from_next_non_halting_neighbour = float.Parse(next_non_halting_station_tds[13].InnerText, CultureInfo.InvariantCulture)
                                                                - float.Parse(tds[13].InnerText, CultureInfo.InvariantCulture);
                        Console.WriteLine("\t" + next_non_halting_station_tds[0].InnerText + "\t" + get_track_type(next_non_halting_station_tds)
                                            + "\t" + next_non_halting_station_tds[2].InnerText + "\t" + tds[2].InnerText
                                            + "\t" + next_non_halting_station_tds[13].InnerText + "\t" + distance_from_next_non_halting_neighbour);
                        sw.WriteLine("\t" + next_non_halting_station_tds[0].InnerText + "\t" + get_track_type(next_non_halting_station_tds)
                                            + "\t" + next_non_halting_station_tds[2].InnerText + "\t" + tds[2].InnerText
                                            + "\t" + next_non_halting_station_tds[13].InnerText + "\t" + distance_from_next_non_halting_neighbour);

                        //function to insert rows in NEIGHBOUR table. Inserted rows have lower lexical station first.
                        fill_neighbour_table(next_non_halting_station_tds[2].InnerText, tds[2].InnerText,
                                                distance_from_next_non_halting_neighbour, get_track_type(next_non_halting_station_tds));
                    }
                }
                else                    //Final Neighbour Pair
                {
                    var tds_intermediate_stations = intermediate_stations[halting_stations.Count - 2].Descendants("td").ToList();
                    var num_intermediate_stations_text = tds_intermediate_stations[3].InnerText;

                    var num_intermediate_stations_pattern = @"[0-9]+";
                    Regex num_intermediate_stations_rgx = new Regex(num_intermediate_stations_pattern, RegexOptions.IgnoreCase);
                    MatchCollection num_intermediate_stations_matches = num_intermediate_stations_rgx.Matches(num_intermediate_stations_text);
                    var num_intermediate_stations = int.Parse(num_intermediate_stations_matches[0].Value);

                    if (num_intermediate_stations > 0)               //if there exists some intermediate station before the last halting station
                    {
                        var final_non_halting_station_tds = non_halting_stations[non_halting_stations.Count - 2].Descendants("td").ToList();
                        var distance_from_final_non_halting_neighbour = float.Parse(tds[13].InnerText, CultureInfo.InvariantCulture)
                                                                - float.Parse(final_non_halting_station_tds[13].InnerText, CultureInfo.InvariantCulture);
                        Console.WriteLine("\t" + tds[0].InnerText + "\t" + get_track_type(tds)
                                            + "\t" + tds[2].InnerText + "\t" + final_non_halting_station_tds[2].InnerText
                                            + "\t" + tds[13].InnerText + "\t" + distance_from_final_non_halting_neighbour);
                        sw.WriteLine("\t" + tds[0].InnerText + "\t" + get_track_type(tds)
                                            + "\t" + tds[2].InnerText + "\t" + final_non_halting_station_tds[2].InnerText
                                            + "\t" + tds[13].InnerText + "\t" + distance_from_final_non_halting_neighbour);

                        //function to insert rows in NEIGHBOUR table. Inserted rows have lower lexical station first.
                        fill_neighbour_table(final_non_halting_station_tds[2].InnerText, tds[2].InnerText,
                                                distance_from_final_non_halting_neighbour, get_track_type(tds));
                    }
                    else                                            //if there does not exist any intermediate station before the last halting station
                    {
                        var last_halting_station_tds = halting_stations[halting_station_num - 1].Descendants("td").ToList();
                        var distance_from_last_halting_neighbour = float.Parse(tds[13].InnerText, CultureInfo.InvariantCulture)
                                                            - float.Parse(last_halting_station_tds[13].InnerText, CultureInfo.InvariantCulture);
                        Console.WriteLine("\t" + tds[0].InnerText + "\t" + get_track_type(tds)
                                        + "\t" + tds[2].InnerText + "\t" + last_halting_station_tds[2].InnerText
                                        + "\t" + tds[13].InnerText + "\t" + distance_from_last_halting_neighbour);
                        sw.WriteLine("\t" + tds[0].InnerText + "\t" + get_track_type(tds)
                                        + "\t" + tds[2].InnerText + "\t" + last_halting_station_tds[2].InnerText
                                        + "\t" + tds[13].InnerText + "\t" + distance_from_last_halting_neighbour);

                        //function to insert rows in NEIGHBOUR table. Inserted rows have lower lexical station first.
                        fill_neighbour_table(last_halting_station_tds[2].InnerText, tds[2].InnerText,
                                                distance_from_last_halting_neighbour, get_track_type(tds));
                    }
                }
            }
            //Get the non-halting stations and their neighbours
            float last_distance = 0;
            int last_station_serial_num = 0;
            float distance_from_last_neighbour = 0;
            string last_station_code = "HHHHH";                 //Dummy initaialization. Would be modified before using.

            Console.WriteLine("\n\n\tNon Halting Stations\n");
            sw.WriteLine("\n\n\tNon Halting Stations\n");

            foreach (var substn in non_halting_stations)
            {
                var tds = substn.Descendants("td").ToList();
                if (tds.Count() > 1)                            //if it is a valid non halting station row
                {
                    var serial_num_text = tds[0].InnerText;
                    var track_type_text = tds[1].InnerText;
                    var station_code_text = tds[2].InnerText;
                    var distance_travelled_text = tds[13].InnerText;

                    var station_serial_num_pattern = @"#[0-9]+";
                    Regex station_serial_num_rgx = new Regex(station_serial_num_pattern, RegexOptions.IgnoreCase);
                    MatchCollection station_serial_num_matches = station_serial_num_rgx.Matches(tds[0].InnerText);
                    var station_serial_num = int.Parse(station_serial_num_matches[0].Value.Substring(1));

                    if (station_serial_num == last_station_serial_num)
                    {
                        distance_from_last_neighbour = float.Parse(distance_travelled_text, CultureInfo.InvariantCulture) - last_distance;
                        Console.WriteLine("\t" + serial_num_text + "\t" + get_track_type(tds) + "\t" + station_code_text
                                            + "\t" + last_station_code + "\t" + tds[13].InnerText + "\t" + distance_from_last_neighbour);
                        sw.WriteLine("\t" + serial_num_text + "\t" + get_track_type(tds) + "\t" + station_code_text
                                            + "\t" + last_station_code + "\t" + tds[13].InnerText + "\t" + distance_from_last_neighbour);

                        //function to insert rows in NEIGHBOUR table. Inserted rows have lower lexical station first.
                        fill_neighbour_table(station_code_text, last_station_code,
                                                distance_from_last_neighbour, get_track_type(tds));
                    }
                    last_distance = float.Parse(distance_travelled_text, CultureInfo.InvariantCulture);
                    last_station_code = station_code_text;
                    last_station_serial_num = station_serial_num;
                }
            }
        }

        //function to insert rows in NEIGHBOUR table. Inserted rows have lower lexical station first.
        public static void fill_neighbour_table(string station_1_code, string station_2_code, float distance, int track_type)
        {
            string first_station_code = station_1_code;
            string second_station_code = station_2_code;
            var lexical_order = String.Compare(station_1_code, station_2_code, true);
            if (lexical_order > 0)
            {
                first_station_code = station_2_code;
                second_station_code = station_1_code;
            }

            SqlConnection conn = new SqlConnection(conn_String);
            conn.Open();
            if (conn.State == System.Data.ConnectionState.Open)
            {
                string sql_query = "select count (*) from NEIGHBOUR where STATION_1_CODE = '" + first_station_code
                                    + "' and STATION_2_CODE = '" + second_station_code + "'";
                SqlCommand cmd_1 = new SqlCommand(sql_query, conn);
                Int32 num_rows = (Int32)cmd_1.ExecuteScalar();

                if (num_rows == 0)
                {
                    sql_query = "insert into NEIGHBOUR values('" + first_station_code + "', '"
                                    + second_station_code + "', '"
                                    + distance + "', '"
                                    + track_type + "')";
                    SqlCommand cmd_2 = new SqlCommand(sql_query, conn);
                    cmd_2.ExecuteNonQuery();
                }
            }
            conn.Close();
        }

        //function to fetch database table "OVERTAKE" when our train is overtaken
        public static void get_overtaken(StreamWriter sw, string train_no, List<HtmlNode> halting_stations, List<HtmlNode> non_halting_stations)
        {
            Console.WriteLine("\t\tHalting stations\n");
            sw.WriteLine("\t\tHalting stations\n");
            foreach (var dark_row in halting_stations)
            {
                var tds = dark_row.Descendants("td").ToList();
                var serial_num_text = tds[0].InnerText;
                var track_type_text = tds[1].InnerText;
                var station_code_text = tds[2].InnerText;
                var xing_html = tds[4].OuterHtml;

                var overtake_pattern = @"Overtaken.*?/span";
                var train_no_pattern = @"[0-9]+";

                Regex rgx = new Regex(overtake_pattern, RegexOptions.IgnoreCase);
                MatchCollection overtake_matches = rgx.Matches(xing_html);

                for (int i = 0; i < overtake_matches.Count; i++)
                {
                    var single_overtake = overtake_matches[i].Value.Substring(0, overtake_matches[i].Value.Length - 9);

                    Regex train_no_rgx = new Regex(train_no_pattern, RegexOptions.IgnoreCase);
                    MatchCollection train_no_matches = train_no_rgx.Matches(single_overtake);
                    Console.Write("\t" + station_code_text + "\t" + train_no_matches[0].Value + "\t" + train_no + "\t");
                    sw.Write("\t" + station_code_text + "\t" + train_no_matches[0].Value + "\t" + train_no + "\t");

                    var single_overtake_days = find_days(single_overtake);
                    foreach (var day in single_overtake_days)
                    {
                        Console.Write(day + " ");
                        sw.Write(day + " ");
                    }

                    //add row to table "OVERTAKE". Former train overtakes.
                    fill_database_table_overtake(train_no_matches[0].Value, train_no, single_overtake_days, station_code_text);

                    Console.WriteLine("");
                    sw.WriteLine("");
                }
            }

            Console.WriteLine("\n\t\tNon Halting stations\n");
            sw.WriteLine("\n\t\tNon Halting stations\n");
            foreach (var substn in non_halting_stations)
            {
                var tds = substn.Descendants("td").ToList();
                if (tds.Count() > 1)
                {
                    var station_code_text = tds[2].InnerText;
                    var overtake_html = tds[4].OuterHtml;

                    var overtake_pattern = @"Overtaken.*?/span";
                    var train_no_pattern = @"[0-9]+";

                    Regex rgx = new Regex(overtake_pattern, RegexOptions.IgnoreCase);
                    MatchCollection overtake_matches = rgx.Matches(overtake_html);

                    for (int i = 0; i < overtake_matches.Count; i++)
                    {
                        var single_overtake = overtake_matches[i].Value.Substring(0, overtake_matches[i].Value.Length - 9);

                        Regex train_no_rgx = new Regex(train_no_pattern, RegexOptions.IgnoreCase);
                        MatchCollection train_no_matches = train_no_rgx.Matches(single_overtake);
                        Console.Write("\t" + station_code_text + "\t" + train_no_matches[0].Value + "\t" + train_no + "\t");
                        sw.Write("\t" + station_code_text + "\t" + train_no_matches[0].Value + "\t" + train_no + "\t");

                        var single_overtake_days = find_days(single_overtake);
                        foreach (var day in single_overtake_days)
                        {
                            Console.Write(day + " ");
                            sw.Write(day + " ");
                        }

                        //add row to table "OVERTAKE". Former train overtakes.
                        fill_database_table_overtake(train_no_matches[0].Value, train_no, single_overtake_days, station_code_text);

                        Console.WriteLine("");
                        sw.WriteLine("");
                    }
                }
            }
        }

        //function to fetch database table "OVERTAKE" when our train overtakes
        public static void get_overtake(StreamWriter sw, string train_no, List<HtmlNode> halting_stations, List<HtmlNode> non_halting_stations)
        {
            Console.WriteLine("\t\tHalting stations\n");
            sw.WriteLine("\t\tHalting stations\n");
            foreach (var dark_row in halting_stations)
            {
                var tds = dark_row.Descendants("td").ToList();
                var serial_num_text = tds[0].InnerText;
                var track_type_text = tds[1].InnerText;
                var station_code_text = tds[2].InnerText;
                var xing_html = tds[4].OuterHtml;

                var overtake_pattern = @"Overtakes.*?/span";
                var train_no_pattern = @"[0-9]+";

                Regex rgx = new Regex(overtake_pattern, RegexOptions.IgnoreCase);
                MatchCollection overtake_matches = rgx.Matches(xing_html);

                for (int i = 0; i < overtake_matches.Count; i++)
                {
                    var single_overtake = overtake_matches[i].Value.Substring(0, overtake_matches[i].Value.Length - 9);

                    Regex train_no_rgx = new Regex(train_no_pattern, RegexOptions.IgnoreCase);
                    MatchCollection train_no_matches = train_no_rgx.Matches(single_overtake);
                    Console.Write("\t" + station_code_text + "\t" + train_no_matches[0].Value + "\t" + train_no + "\t");
                    sw.Write("\t" + station_code_text + "\t" + train_no_matches[0].Value + "\t" + train_no + "\t");

                    var single_overtake_days = find_days(single_overtake);
                    foreach (var day in single_overtake_days)
                    {
                        Console.Write(day + " ");
                        sw.Write(day + " ");
                    }

                    //add row to table "OVERTAKE". Former train overtakes.
                    fill_database_table_overtake(train_no, train_no_matches[0].Value, single_overtake_days, station_code_text);

                    Console.WriteLine("");
                    sw.WriteLine("");
                }
            }

            Console.WriteLine("\n\t\tNon Halting stations\n");
            sw.WriteLine("\n\t\tNon Halting stations\n");
            foreach (var substn in non_halting_stations)
            {
                var tds = substn.Descendants("td").ToList();
                if (tds.Count() > 1)
                {
                    var station_code_text = tds[2].InnerText;
                    var overtake_html = tds[4].OuterHtml;

                    var overtake_pattern = @"Overtakes.*?/span";
                    var train_no_pattern = @"[0-9]+";

                    Regex rgx = new Regex(overtake_pattern, RegexOptions.IgnoreCase);
                    MatchCollection overtake_matches = rgx.Matches(overtake_html);

                    for (int i = 0; i < overtake_matches.Count; i++)
                    {
                        var single_overtake = overtake_matches[i].Value.Substring(0, overtake_matches[i].Value.Length - 9);

                        Regex train_no_rgx = new Regex(train_no_pattern, RegexOptions.IgnoreCase);
                        MatchCollection train_no_matches = train_no_rgx.Matches(single_overtake);
                        Console.Write("\t" + station_code_text + "\t" + train_no_matches[0].Value + "\t" + train_no + "\t");
                        sw.Write("\t" + station_code_text + "\t" + train_no_matches[0].Value + "\t" + train_no + "\t");

                        var single_overtake_days = find_days(single_overtake);
                        foreach (var day in single_overtake_days)
                        {
                            Console.Write(day + " ");
                            sw.Write(day + " ");
                        }

                        //add row to table "OVERTAKE". Former train overtakes.
                        fill_database_table_overtake(train_no, train_no_matches[0].Value, single_overtake_days, station_code_text);

                        Console.WriteLine("");
                        sw.WriteLine("");
                    }
                }
            }
        }

        //function to insert rows in OVERTAKE table. train_1 overtakes train_2.
        public static void fill_database_table_overtake(string train_1_code, string train_2_code, char[] days, string station_code_text)
        {
            SqlConnection conn = new SqlConnection(conn_String);
            conn.Open();
            if (conn.State == System.Data.ConnectionState.Open)
            {
                string sql_query = "select count (*) from OVERTAKE where TRAIN_1_CODE = '" + train_1_code
                                    + "' and TRAIN_2_CODE = '" + train_2_code + "'";
                SqlCommand cmd_1 = new SqlCommand(sql_query, conn);
                Int32 num_rows = (Int32)cmd_1.ExecuteScalar();

                if (num_rows == 0)
                {
                    sql_query = "insert into OVERTAKE values('" + train_1_code + "', '"
                                    + train_2_code + "', '" + days[0] + "', '"
                                    + days[1] + "', '" + days[2] + "', '" + days[3] + "', '"
                                    + days[4] + "', '" + days[5] + "', '" + days[6] + "', '"
                                    + station_code_text + "')";
                    SqlCommand cmd_2 = new SqlCommand(sql_query, conn);
                    cmd_2.ExecuteNonQuery();
                }
            }
            conn.Close();
        }

        //function to fetch database table "XING"
        public static void get_xings(StreamWriter sw, string train_no, List<HtmlNode> halting_stations, List<HtmlNode> non_halting_stations)
        {
            Console.WriteLine("\t\tHalting stations\n");
            sw.WriteLine("\t\tHalting stations\n");
            foreach (var dark_row in halting_stations)
            {
                var tds = dark_row.Descendants("td").ToList();
                var serial_num_text = tds[0].InnerText;
                var track_type_text = tds[1].InnerText;
                var station_code_text = tds[2].InnerText;
                var xing_html = tds[4].OuterHtml;

                var xing_pattern = @"Xing.*?/span";
                var train_no_pattern = @"[0-9]+";

                Regex rgx = new Regex(xing_pattern, RegexOptions.IgnoreCase);
                MatchCollection xing_matches = rgx.Matches(xing_html);

                for (int i = 0; i < xing_matches.Count; i++)
                {
                    var single_xing = xing_matches[i].Value.Substring(0, xing_matches[i].Value.Length - 9);

                    Regex train_no_rgx = new Regex(train_no_pattern, RegexOptions.IgnoreCase);
                    MatchCollection train_no_matches = train_no_rgx.Matches(single_xing);
                    Console.Write("\t" + station_code_text + "\t" + train_no_matches[0].Value + "\t" + train_no + "\t");
                    sw.Write("\t" + station_code_text + "\t" + train_no_matches[0].Value + "\t" + train_no + "\t");

                    var single_xing_days = find_days(single_xing);
                    foreach (var day in single_xing_days)
                    {
                        Console.Write(day + " ");
                        sw.Write(day + " ");
                    }

                    //add row to table "XING". Train with lower lexical order takes first column.
                    fill_database_table_xing(train_no, train_no_matches[0].Value, single_xing_days, station_code_text);

                    Console.WriteLine("");
                    sw.WriteLine("");
                }
            }

            Console.WriteLine("\n\t\tNon Halting stations\n");
            sw.WriteLine("\n\t\tNon Halting stations\n");
            foreach (var substn in non_halting_stations)
            {
                var tds = substn.Descendants("td").ToList();
                if (tds.Count() > 1)
                {
                    var serial_num_text = tds[0].InnerText;
                    var track_type_text = tds[1].InnerText;
                    var station_code_text = tds[2].InnerText;
                    var xing_html = tds[4].OuterHtml;

                    var xing_pattern = @"Xing.*?/span";
                    var train_no_pattern = @"[0-9]+";

                    Regex rgx = new Regex(xing_pattern, RegexOptions.IgnoreCase);
                    MatchCollection xing_matches = rgx.Matches(xing_html);

                    for (int i = 0; i < xing_matches.Count; i++)
                    {
                        var single_xing = xing_matches[i].Value.Substring(0, xing_matches[i].Value.Length - 9);

                        Regex train_no_rgx = new Regex(train_no_pattern, RegexOptions.IgnoreCase);
                        MatchCollection train_no_matches = train_no_rgx.Matches(single_xing);
                        Console.Write("\t" + station_code_text + "\t" + train_no_matches[0].Value + "\t" + train_no + "\t");
                        sw.Write("\t" + station_code_text + "\t" + train_no_matches[0].Value + "\t" + train_no + "\t");

                        var single_xing_days = find_days(single_xing);
                        foreach (var day in single_xing_days)
                        {
                            Console.Write(day + " ");
                            sw.Write(day + " ");
                        }

                        //add row to table "XING". Train with lower lexical order takes first column.
                        fill_database_table_xing(train_no, train_no_matches[0].Value, single_xing_days, station_code_text);

                        Console.WriteLine("");
                        sw.WriteLine("");
                    }
                }
            }
        }

        //function to insert rows in XING table. Inserted rows have lower lexical train first.
        public static void fill_database_table_xing(string train_1_code, string train_2_code, char[] days, string station_code_text)
        {
            string first_train_code = train_1_code;
            string second_train_code = train_2_code;
            var lexical_order = String.Compare(train_1_code, train_2_code, true);
            if (lexical_order > 0)
            {
                first_train_code = train_2_code;
                second_train_code = train_1_code;
            }

            SqlConnection conn = new SqlConnection(conn_String);
            conn.Open();
            if (conn.State == System.Data.ConnectionState.Open)
            {
                string sql_query = "select count (*) from XING where TRAIN_1_CODE = '" + first_train_code
                                    + "' and TRAIN_2_CODE = '" + second_train_code + "'";
                SqlCommand cmd_1 = new SqlCommand(sql_query, conn);
                Int32 num_rows = (Int32)cmd_1.ExecuteScalar();

                if (num_rows == 0)
                {
                    sql_query = "insert into XING values('" + first_train_code + "', '"
                                    + second_train_code + "', '" + days[0] + "', '"
                                    + days[1] + "', '" + days[2] + "', '" + days[3] + "', '"
                                    + days[4] + "', '" + days[5] + "', '" + days[6] + "', '"
                                    + station_code_text + "')";
                    SqlCommand cmd_2 = new SqlCommand(sql_query, conn);
                    cmd_2.ExecuteNonQuery();
                }
            }
            conn.Close();
        }

        //function to fetch database table "TRAIN"
        public static void get_train_table(StreamWriter sw, List<HtmlNode> divs_topcapsule, List<HtmlNode> halting_stations, List<HtmlNode> divs_listingcapsulehalf)
        {
            //Train number
            var train_no = (divs_topcapsule[0].Descendants("h1").ToList())[0].InnerText.Substring(0, 5);

            //Train Type
            var train_type = get_train_type(divs_listingcapsulehalf[0].Descendants("tr").ToList()[3].OuterHtml);
            
            //List of days at which train departs from the Source Station
            var departure_days = ((divs_listingcapsulehalf[0].Descendants("tr").ToList())[4].Descendants("td")).ToList();

            //Source Station
            var src_station = (halting_stations[0].Descendants("td").ToList())[2].InnerText;

            //Destination Station
            var dst_station = (halting_stations[halting_stations.Count - 1].Descendants("td").ToList())[2].InnerText;

            //Length of Jouney
            var length_of_journey = (halting_stations[halting_stations.Count - 1].Descendants("td").ToList())[13].InnerText;

            //Average Speed
            var avg_speed = divs_listingcapsulehalf[0].Descendants("tr").ToList()[1].Descendants("td").ToList()[0].InnerText;
            avg_speed = avg_speed.Substring(avg_speed.IndexOf("Avg Speed"));
            avg_speed = avg_speed.Substring(avg_speed.IndexOf(":") + 2);
            avg_speed = avg_speed.Substring(0, avg_speed.IndexOf(" "));

            Console.WriteLine("\n\n\t\tTrain No             :   " + train_no);
            sw.WriteLine("\n\n\t\tTrain No             :   " + train_no);
            Console.Write("\n\n\t\tDays of Departure    :   ");
            sw.Write("\n\n\t\tDays of Departure    :   ");
            char[] days = get_departure_days(sw, departure_days);
            Console.WriteLine("\n\n\t\tSource Station       :   " + src_station);
            Console.WriteLine("\n\n\t\tDestination Station  :   " + dst_station);
            Console.WriteLine("\n\n\t\tLength of Journey    :   " + length_of_journey);
            Console.WriteLine("\n\n\t\tAverage Speed        :   " + avg_speed);
            Console.WriteLine("\n\n\t\tTrain Type           :   " + train_type);

            sw.WriteLine("\n\n\t\tSource Station       :   " + src_station);
            sw.WriteLine("\n\n\t\tDestination Station  :   " + dst_station);
            sw.WriteLine("\n\n\t\tLength of Journey    :   " + length_of_journey);
            sw.WriteLine("\n\n\t\tAverage Speed        :   " + avg_speed);

            SqlConnection conn = new SqlConnection(conn_String);
            conn.Open();
            if (conn.State == System.Data.ConnectionState.Open)
            {

                string sql_query = "select count (*) from TRAIN where TRAIN_NO = '" + train_no + "'";
                SqlCommand cmd_1 = new SqlCommand(sql_query, conn);
                Int32 num_rows = (Int32)cmd_1.ExecuteScalar();

                if (num_rows == 0)
                {
                    sql_query = "insert into TRAIN values('" + train_no + "', '" + days[0] + "', '"
                                    + days[1] + "', '" + days[2] + "', '" + days[3] + "', '"
                                    + days[4] + "', '" + days[5] + "', '" + days[6] + "', '"
                                    + src_station + "', '" + dst_station + "', '"
                                    + float.Parse(length_of_journey) + "', '"
                                    + float.Parse(avg_speed) + "', '"
                                    + train_type + "')";
                    SqlCommand cmd_2 = new SqlCommand(sql_query, conn);
                    cmd_2.ExecuteNonQuery();
                }
            }
            conn.Close();
        }

        //fetch train type
        public static int get_train_type(string train_type_text)
        {
            int train_type = 10;

            if (train_type_text.Contains("Shatabdi"))
                train_type = 0;
            else if(train_type_text.Contains("Rajdhani"))
                train_type = 1;
            else if (train_type_text.Contains("Duronto"))
                train_type = 2;
            else if (train_type_text.Contains("Jan Shatabdi"))
                train_type = 3;
            else if (train_type_text.Contains("Suvidha"))
                train_type = 4;
            else if (train_type_text.Contains("Garib Rath"))
                train_type = 5;
            else if (train_type_text.Contains("Sampark Kranti"))
                train_type = 6;
            else if (train_type_text.Contains("AC SuperFast"))
                train_type = 7;
            else if (train_type_text.Contains("AC Express"))
                train_type = 8;
            else if (train_type_text.Contains("SuperFast"))
                train_type = 9;
            else if (train_type_text.Contains("Mail/Express"))
                train_type = 9;
            else if (train_type_text.Contains("Passenger"))
                train_type = 11;
            else if (train_type_text.Contains("MEMU"))
                train_type = 12;
            else if (train_type_text.Contains("DEMU"))
                train_type = 13;

            return train_type;
        }

        //Fetch each day from "Departure Days" List
        public static char[] get_departure_days(StreamWriter sw, List<HtmlNode> departure_days)
        {
            char[] days = { 'N', 'N', 'N', 'N', 'N', 'N', 'N' };
            int i = 0;
            foreach (var day in departure_days)
            {
                int ascii_val = (day.InnerText)[0];
                if (ascii_val != 160)
                    days[i] = day.InnerText[0];
                Console.Write(" " + days[i]);
                sw.Write(" " + days[i]);
                i++;
            }
            return days;
        }

        //Fetch details at stations where the train halts
        public static void get_halting_stations_details(StreamWriter sw, string train_no, List<HtmlNode> halting_stations)
        {
            int num_halting_stations_visited = 0;
            foreach (var dark_row in halting_stations)
            {
                num_halting_stations_visited++;
                var tds = dark_row.Descendants("td").ToList();
                var serial_num_text = tds[0].InnerText;
                var track_type_text = tds[1].OuterHtml;
                var track_type_pattern = @"title1=.*><span";
                Regex track_type_rgx = new Regex(track_type_pattern, RegexOptions.IgnoreCase);
                MatchCollection track_type_matches = track_type_rgx.Matches(track_type_text);
                track_type_text = (track_type_matches[0].Value).Substring(8);
                track_type_text = track_type_text.Substring(0, track_type_text.Length - 7);
                var station_code_text = tds[2].InnerText;
                var no_of_platforms = tds[2].InnerHtml;
                var pattern = @"[0-9]+( )(PFs)";
                Regex rgx = new Regex(pattern, RegexOptions.IgnoreCase);
                MatchCollection matches = rgx.Matches(no_of_platforms);
                if (matches.Count > 0)
                    no_of_platforms = matches[0].Value.Substring(0, matches[0].Value.IndexOf(" "));
                else
                    no_of_platforms = "0";
                var station_name_text = tds[3].InnerText;
                if (station_name_text.Contains("&"))
                {
                    var temp_station_name_text = "";
                    foreach (var ch in station_name_text)
                    {
                        if (ch == '&')
                            break;
                        temp_station_name_text = temp_station_name_text + ch;
                    }
                    station_name_text = temp_station_name_text;
                }
                var arrival_time_text = tds[6].InnerText;
                if (arrival_time_text == "")
                {
                    DateTime dep_time = DateTime.ParseExact(tds[8].InnerText, "HH:mm", CultureInfo.CurrentCulture);
                    DateTime arr_time = dep_time.Subtract(TimeSpan.FromMinutes(30));
                    arrival_time_text = arr_time.ToString("HH:mm", CultureInfo.CurrentCulture);
                }
                var departure_time_text = tds[8].InnerText;
                if (departure_time_text == "")
                {
                    DateTime arr_time = DateTime.ParseExact(arrival_time_text, "HH:mm", CultureInfo.CurrentCulture);
                    DateTime dep_time = arr_time.Add(TimeSpan.FromMinutes(30));
                    departure_time_text = dep_time.ToString("HH:mm", CultureInfo.CurrentCulture);
                }
                var platform_no_text = tds[11].InnerText;
                var arrival_day_text = tds[12].InnerText;
                var departure_day_text = tds[12].InnerText;
                if (num_halting_stations_visited == 1)
                {
                    //  Source Station
                }
                else
                {
                    if (String.Compare(arrival_time_text, departure_time_text, true) > 0)
                        departure_day_text = "" + (int.Parse(arrival_day_text) + 1);
                }
                var distance_travelled_text = tds[13].InnerText;
                var elevation_text = tds[15].InnerText;
                if (elevation_text.Contains("m"))
                    elevation_text = elevation_text.Substring(0, elevation_text.Length - 1);
                else
                    elevation_text = "-1";
                Console.WriteLine("\t" + serial_num_text + "\t" + track_type_text + "\t" + station_code_text + "\t"
                                    + arrival_time_text + "\t" + departure_time_text + "\t" + platform_no_text + "\t" + arrival_day_text
                                    + "\t" + departure_day_text + "\t" + tds[13].InnerText + "\t" + elevation_text + "\t" + no_of_platforms);
                sw.WriteLine("\t" + serial_num_text + "\t" + track_type_text + "\t" + station_code_text + "\t"
                                    + arrival_time_text + "\t" + departure_time_text + "\t" + platform_no_text + "\t" + arrival_day_text
                                    + "\t" + departure_day_text + "\t" + tds[13].InnerText + "\t" + elevation_text + "\t" + no_of_platforms);

                SqlConnection conn = new SqlConnection(conn_String);
                conn.Open();
                if (conn.State == System.Data.ConnectionState.Open)
                {
                    string sql_query = "select count (*) from STATION where STATION_CODE = '" + station_code_text + "'";
                    SqlCommand cmd_1 = new SqlCommand(sql_query, conn);
                    Int32 num_rows = (Int32)cmd_1.ExecuteScalar();

                    if (num_rows == 0)
                    {
                        sql_query = "insert into STATION values('" + station_code_text + "', '"
                                        + int.Parse(no_of_platforms) + "', '"
                                        + float.Parse(elevation_text) + "')";
                        SqlCommand cmd_2 = new SqlCommand(sql_query, conn);
                        cmd_2.ExecuteNonQuery();
                    }

                    sql_query = "select count (*) from USES where STATION_CODE = '" + station_code_text
                                + "'and TRAIN_NO = '" + train_no + "'";
                    SqlCommand cmd_3 = new SqlCommand(sql_query, conn);
                    num_rows = (Int32)cmd_3.ExecuteScalar();

                    if (num_rows == 0)
                    {
                        sql_query = "insert into USES values('" + train_no + "', '"
                                        + station_code_text + "', '"
                                        + arrival_time_text + "', '"
                                        + departure_time_text + "', '"
                                        + int.Parse(arrival_day_text) + "', '"
                                        + int.Parse(departure_day_text) + "', '"
                                        + float.Parse(distance_travelled_text) + "')";
                        SqlCommand cmd_4 = new SqlCommand(sql_query, conn);
                        cmd_4.ExecuteNonQuery();
                    }
                }
                conn.Close();
            }
        }

        //Fetch Details at stations where the train does not halt
        public static void get_non_halting_stations_details(StreamWriter sw, string train_no, List<HtmlNode> non_halting_stations)
        {
            foreach (var substn in non_halting_stations)
            {
                var tds = substn.Descendants("td").ToList();
                if (tds.Count() > 1)
                {
                    var serial_num_text = tds[0].InnerText;
                    var track_type_text = tds[1].OuterHtml;
                    var track_type_pattern = @"title1=.*><span";
                    Regex track_type_rgx = new Regex(track_type_pattern, RegexOptions.IgnoreCase);
                    MatchCollection track_type_matches = track_type_rgx.Matches(track_type_text);
                    track_type_text = (track_type_matches[0].Value).Substring(8);
                    track_type_text = track_type_text.Substring(0, track_type_text.Length - 7);
                    var station_code_text = tds[2].InnerText;
                    var no_of_platforms = tds[2].InnerHtml;
                    var pattern = @"[0-9]+( )(PFs)";
                    Regex rgx = new Regex(pattern, RegexOptions.IgnoreCase);
                    MatchCollection matches = rgx.Matches(no_of_platforms);
                    if (matches.Count > 0)
                        no_of_platforms = matches[0].Value.Substring(0, matches[0].Value.IndexOf(" "));
                    else
                        no_of_platforms = "0";
                    var station_name_text = tds[3].InnerText;
                    if (station_name_text.Contains("&"))
                    {
                        var temp_station_name_text = "";
                        foreach (var ch in station_name_text)
                        {
                            if (ch == '&')
                                break;
                            temp_station_name_text = temp_station_name_text + ch;
                        }
                        station_name_text = temp_station_name_text;
                    }
                    var arrival_time_text = tds[6].InnerText;
                    var departure_time_text = tds[8].InnerText;
                    var platform_no_text = tds[11].InnerText;
                    var arrival_day_text = tds[12].InnerText;
                    var departure_day_text = tds[12].InnerText;
                    var distance_travelled_text = tds[13].InnerText;
                    var elevation_text = tds[15].InnerText;
                    if (elevation_text.Contains("m"))
                        elevation_text = elevation_text.Substring(0, elevation_text.Length - 1);
                    else
                        elevation_text = "-1";

                    Console.WriteLine("\t" + serial_num_text + "\t" + track_type_text + "\t" + station_code_text + "\t"
                                        + "\t" + arrival_time_text + "\t" + departure_time_text + "\t" + platform_no_text + "\t" + arrival_day_text
                                    + "\t" + departure_day_text + "\t" + tds[13].InnerText + "\t" + elevation_text + "\t" + no_of_platforms);
                    sw.WriteLine("\t" + serial_num_text + "\t" + track_type_text + "\t" + station_code_text + "\t"
                                        + "\t" + arrival_time_text + "\t" + departure_time_text + "\t" + platform_no_text + "\t" + arrival_day_text
                                        + "\t" + departure_day_text + "\t" + tds[13].InnerText + "\t" + elevation_text + "\t" + no_of_platforms);

                    SqlConnection conn = new SqlConnection(conn_String);
                    conn.Open();
                    if (conn.State == System.Data.ConnectionState.Open)
                    {
                        string sql_query = "select count (*) from STATION where STATION_CODE = '" + station_code_text + "'";
                        SqlCommand cmd_1 = new SqlCommand(sql_query, conn);
                        Int32 num_rows = (Int32)cmd_1.ExecuteScalar();

                        if (num_rows == 0)
                        {
                            sql_query = "insert into STATION values('" + station_code_text + "', '"
                                            + int.Parse(no_of_platforms) + "', '"
                                            + float.Parse(elevation_text) + "')";
                            SqlCommand cmd_2 = new SqlCommand(sql_query, conn);
                            cmd_2.ExecuteNonQuery();
                        }

                        sql_query = "select count (*) from USES where STATION_CODE = '" + station_code_text
                                    + "'and TRAIN_NO = '" + train_no + "'";
                        SqlCommand cmd_3 = new SqlCommand(sql_query, conn);
                        num_rows = (Int32)cmd_3.ExecuteScalar();

                        if (num_rows == 0)
                        {
                            sql_query = "insert into USES values('" + train_no + "', '"
                                            + station_code_text + "', '"
                                            + arrival_time_text + "', '"
                                            + departure_time_text + "', '"
                                            + int.Parse(arrival_day_text) + "', '"
                                            + int.Parse(departure_day_text) + "', '"
                                            + float.Parse(distance_travelled_text) + "')";
                            SqlCommand cmd_4 = new SqlCommand(sql_query, conn);
                            cmd_4.ExecuteNonQuery();
                        }
                    }
                    conn.Close();
                }
            }
        }

        //return the days as an array of characters after parsing the string "day_schedule" which contains associated days
        public static char[] find_days(string day_schedule)
        {
            char[] days = { 'N', 'N', 'N', 'N', 'N', 'N', 'N' };

            if (day_schedule.Contains("Sun") || day_schedule.Contains("Daily"))
                days[0] = 'S';
            if (day_schedule.Contains("Mon") || day_schedule.Contains("Daily"))
                days[1] = 'M';
            if (day_schedule.Contains("Tue") || day_schedule.Contains("Daily"))
                days[2] = 'T';
            if (day_schedule.Contains("Wed") || day_schedule.Contains("Daily"))
                days[3] = 'W';
            if (day_schedule.Contains("Thu") || day_schedule.Contains("Daily"))
                days[4] = 'T';
            if (day_schedule.Contains("Fri") || day_schedule.Contains("Daily"))
                days[5] = 'F';
            if (day_schedule.Contains("Sat") || day_schedule.Contains("Daily"))
                days[6] = 'S';

            return days;
        }
        
        //  function returns time in DateTime type using argument time in string form "hh:mm"
        public DateTime get_time(String time)
        {
            return DateTime.ParseExact(time, "HH:mm", CultureInfo.CurrentCulture);
        }
    }
}