/*

    This class has a method "public List<train> Schedule(Argument List)" which takes
    various parameters. The parameters are Source Station, Destination Station,
    Source Station Departure Time, Destination Station Arrival Time, List of
    Intermediate Halting Stations and halt durations, Days of Departure from the
    source station.

    It returns a list of possible train itineraries each of which consists of a list 
    of rows. Each of these rows has neighbouring station paired. Each pairing stores
    departure station code, departure time at departure station, arrival 
    station code, arrival time at arrival station, arrival day number at 
    arrival station and number of platforms at arrival station. All these 
    rows together form the itinerary of the train.

    This program considers only congestion levels at the intermediate stations 
    at concerned times to compute a schedule with the given parameters.

    Congestions at tracks is not considered.

*/

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Data.SqlClient;

namespace Train_Scheduler_2
{

    //  structure of object to fetch itinerary of our train, last station 
    //  in our itineary and arrival time at destination
    struct path_complete
    {
        public DateTime arrival_time_at_destination;    //  arrival time at the last station of our itinerary
        public String destination_station_code;         //  last station of our tarin itinerary
        public train our_train;                         //  complete itinerary of our train
        public float sum_of_occupied_to_total_tracks;   //  summation of occupied tracks to total no of tracks at each station
    }

    //  structure of the rows of inter-station movement, only
    //  between neighbouring stations along the route
    struct row
    {
        public string station_1_code;               //  departure station code
        public string station_2_code;               //  arrival station code
        public DateTime station_1_departure_time;   //  departure time at departure station
        public DateTime station_2_arrival_time;     //  arrival time at arrival station
        public int arrival_day_num_at_station_2;    //  arrival day number at arrival station
        public int num_platforms_at_station_2;      //  number of platforms at arrival station
    }

    //  structure of the train itinerary
    //  it conatins a list of pairing of neighbouring
    //  stations along the route
    struct train
    {
        public List<row> station_pairs;             //  inter-station pairs (neighbours only along the route)
    }

    //  structure of object to return our train itinerary and
    //  number of trains that are departing from the source 
    //  station towards the direction same as that of our train
    struct pair_num_trains_in_same_direction_and_train_table
    {
        public int num_trains_in_same_direction;
        public train our_train;                         //  complete itinerary of our train
        public float sum_of_occupied_to_total_tracks;   //  summation of occupied tracks to total no of tracks at each station
    }

    //  
    //    
    struct pair_time_and_overlapping_trains
    {
        public DateTime time;
        public List<String> trains;
    }





    class Scheduler
    {
        //  Connection to database
        public static string conn_String = "Data Source=(localdb)\\Projects;Initial Catalog=Train_Database_1;Integrated Security=True;MultipleActiveResultSets=True;";

        //  Connection to a temporary file to show logs
        StreamWriter mw = new StreamWriter("D:\\Documents\\IIT Patna\\ACADEMIC\\SEMESTER VII\\BTP\\schedule_temp.txt");

        //  Connection to a demo file to show solution itineraries
        StreamWriter demo = new StreamWriter("D:\\Documents\\IIT Patna\\ACADEMIC\\SEMESTER VII\\BTP\\schedule_demo.txt");
        
        //  stores train numbers of all distinct trains that overlap with our train (trains running in same direction as ours)
        List<String> list_of_overlapping_trains = new List<string>();

        //  function to return the possible train itineraries according to the passed arguments
        public List<train> Schedule(String source_station_code, String destination_station_code, String source_station_departure_time,
                                String destination_station_arrival_time, String[] intermediate_halting_stations,
                                String[] halt_duration_at_intermediate_halting_stations, char[] days_of_departure_from_source,
                                int arrival_day_num_at_destination_station, float avg_speed, int train_type)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();

            var station_code = source_station_code;

            //  possible train itineraries
            List<train> possible_trains = new List<train>();

            //  most recommended train
            pair_num_trains_in_same_direction_and_train_table recommended_train = new pair_num_trains_in_same_direction_and_train_table();

            //  initialized as -1 to show no recommended train is found yet
            recommended_train.sum_of_occupied_to_total_tracks = -1;

            SqlConnection conn = new SqlConnection(conn_String);
            conn.Open();
            if (conn.State == System.Data.ConnectionState.Open)
            {
                //  Consider a window of 60 minutes for departure. Window starts 30 minutes prior to 
                //  departure of our train and ends 30 minutes after departure of our train.  
                String begin_time = (get_time(source_station_departure_time).Subtract(TimeSpan.FromMinutes(30))).ToString("HH:mm", CultureInfo.CurrentCulture);
                String finish_time = (get_time(source_station_departure_time).Add(TimeSpan.FromMinutes(30))).ToString("HH:mm", CultureInfo.CurrentCulture);
                Console.WriteLine("\n\t" + begin_time + "\t" + finish_time + "\n");

                //  maximum duration of the journey, can be relaxed if needed
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

                //  function fetches no of platforms at the given station
                int no_of_platforms = get_no_of_platforms(station_code);
                int no_of_through_tracks = 2;

                //  function returns the shortest path as a List of String of Station Codes between given stations (end stations inclusive)
                List<String> list_of_stations = get_shortest_path(source_station_code, destination_station_code, intermediate_halting_stations);

                //  length of the journey
                double total_journey_length = find_total_length_of_journey(list_of_stations);

                //  time to comlete the journey
                TimeSpan time_to_complete_journey = TimeSpan.FromMinutes(0);
                if (arrival_day_num_at_destination_station == 1)
                    time_to_complete_journey = get_time(destination_station_arrival_time) - get_time(source_station_departure_time);
                else
                {
                    time_to_complete_journey = get_time(destination_station_arrival_time) - get_time(source_station_departure_time);
                    time_to_complete_journey = time_to_complete_journey.Add(TimeSpan.FromHours(24 * (arrival_day_num_at_destination_station - 1)));
                }

                //  at source train, assume train needs to stay at a platform 
                //  for 30 minutes for boarding purposes before departure

                //  lower limit of time for the train to begin halting at the source station
                DateTime begin_time_lower_limit = get_time(source_station_departure_time).Subtract(TimeSpan.FromMinutes(60));

                //  upper limit of time for the train to begin halting at the source station
                DateTime begin_time_upper_limit = get_time(source_station_departure_time).Add(TimeSpan.FromMinutes(0));

                DateTime current_time = begin_time_lower_limit;

                while (current_time <= begin_time_upper_limit)
                {
                    String halt_period_start = (current_time.Subtract(TimeSpan.FromMinutes(5))).ToString("HH:mm", CultureInfo.CurrentCulture);
                    String halt_period_end = (current_time.Add(TimeSpan.FromMinutes(35))).ToString("HH:mm", CultureInfo.CurrentCulture);

                    //  find source station departure time in "DateTime" fromat
                    DateTime src_station_departure_time = current_time + TimeSpan.FromMinutes(30);

                    //  function returns list of train codes that overlap the given time range at given station on concerned week days
                    List<String> overlapping_trains = get_list_of_trains_overlapping_on_concerned_days_and_time_range(days_of_departure_from_source,
                                                        halt_period_start, halt_period_end, station_code);

                    //  fetch number of overlapping trains on the day
                    //  with maximum number of overlappings
                    int maximum_no_of_overlaps = get_maximum_no_of_overlaps(overlapping_trains, station_code);

                    //  overlapping trains which halt at the current station
                    List<String> halting_overlapping_trains = find_overlapping_trains_that_halt(overlapping_trains, station_code);

                    //  fetch number of halting overlapping trains on the day
                    //  with maximum number of overlappings
                    int maximum_no_of_overlaps_for_halting_trains = get_maximum_no_of_overlaps(halting_overlapping_trains, station_code);

                    Console.WriteLine("\n\tCurrent Time  :  " + (current_time + TimeSpan.FromMinutes(30)));
                    mw.WriteLine("\n\tCurrent Time  :  " + (current_time + TimeSpan.FromMinutes(30)));
                    var current_station_code = source_station_code;

                    //  check if there is a platform available for our train for departure
                    //  and also in total there is a track available for all overlapping trains
                    if (maximum_no_of_overlaps_for_halting_trains < no_of_platforms &&
                            maximum_no_of_overlaps < no_of_platforms + no_of_through_tracks)
                    {
                        //  a composite object to fetch our train itinerary and number of trains that are 
                        //  departing from the source station towards the direction same as that of our train
                        //  -1 as number of trains in same direction is a flag that shows a desired path has been found
                        pair_num_trains_in_same_direction_and_train_table num_trains_in_same_direction = find_path_after_source_station(current_time,
                                                            maximum_duration_of_journey, current_station_code, days_of_departure_from_source, list_of_stations,
                                                            intermediate_halting_stations, halt_duration_at_intermediate_halting_stations, avg_speed, 
                                                            total_journey_length, time_to_complete_journey, src_station_departure_time, train_type);

                        //  num_trains_in_same_direction is intentionally returned as -1
                        //  to show that a desired path has been found
                        if (num_trains_in_same_direction.num_trains_in_same_direction == -1)
                        {
                            possible_trains.Add(num_trains_in_same_direction.our_train);

                            //  if there is no recommended train yet
                            if (recommended_train.sum_of_occupied_to_total_tracks == -1)
                                recommended_train = num_trains_in_same_direction;

                            //  if a lower summation of occupied to total tracks is found
                            if (recommended_train.sum_of_occupied_to_total_tracks > num_trains_in_same_direction.sum_of_occupied_to_total_tracks)
                                recommended_train = num_trains_in_same_direction;

                        }

                        //  if a train is already departing at current_time + 30 minutes (which 
                        //  would be departure time of our train), next try after 10 minutes
                        if (num_trains_in_same_direction.num_trains_in_same_direction > 0)
                        {
                            //  1 minute will be added later on which is being added by default
                            current_time = current_time.Add(TimeSpan.FromMinutes(9));
                        }
                    }
                    else            //  not much room available for our train
                    {
                        Console.WriteLine("\n\t\t\tToo Many Trains. No. of Trains = " + maximum_no_of_overlaps_for_halting_trains);
                        mw.WriteLine("\n\t\t\t\t\t\tToo Many Trains. No. of Trains = " + overlapping_trains.Count);
                    }

                    //  if no train is departing at current_time + 30 minutes (which 
                    //  would be departure time of our train), next try after 1 minute
                    current_time = current_time.Add(TimeSpan.FromMinutes(1));
                }

                watch.Stop();
                var elapsed_time = watch.Elapsed;

                demo.WriteLine("\n\n\n\t\t\t\tRun Time (in hh:mm:ss)\t:\t" + elapsed_time + "\n");

                mw.WriteLine("\n\n\n\t\t\t\t\tTotal Possible Schedules\t:\t" + possible_trains.Count());
                demo.WriteLine("\n\n\t\t\t\tTotal Possible Schedules\t:\t" + possible_trains.Count() + "\n");
                foreach (var train in possible_trains)
                {
                    int i = 0;
                    float distance_travelled = 0;
                    DateTime arrival_time = train.station_pairs[0].station_1_departure_time;
                    int arrival_day_num = 0;
                    String station_2_code = "";

                    mw.WriteLine("\n\n\tStation Wise Schedule\n");

                    foreach (var row in train.station_pairs)
                    {
                        i++;
                        if (i == 1)
                        {
                            mw.Write("\n\t" + i + "\t" + row.station_1_code + "\t");
                            if (row.station_1_code.Length <= 3)
                                mw.Write("\t");
                            mw.Write((row.station_1_departure_time - TimeSpan.FromMinutes(30))
                                        + "\t" + row.station_1_departure_time + "\t" + TimeSpan.FromMinutes(30)
                                        + "\t" + distance_travelled.ToString("0.00") + "\t" + (arrival_day_num + 1) + "\n");
                        }
                        else
                        {
                            mw.Write("\n\t" + i + "\t" + row.station_1_code + "\t");
                            if (row.station_1_code.Length <= 3)
                                mw.Write("\t");
                            mw.Write(arrival_time + "\t" + row.station_1_departure_time + "\t" + (row.station_1_departure_time - arrival_time)
                                        + "\t" + distance_travelled.ToString("0.00") + "\t" + arrival_day_num + "\n");
                        }

                        distance_travelled = distance_travelled + (float)get_distance_between_neighbouring_stations(row.station_1_code, row.station_2_code);
                        arrival_time = row.station_2_arrival_time;
                        arrival_day_num = row.arrival_day_num_at_station_2;
                        station_2_code = row.station_2_code;
                    }

                    mw.Write("\n\t" + (i + 1) + "\t" + station_2_code + "\t");
                    if (station_2_code.Length <= 3)
                        mw.Write("\t");
                    mw.Write(arrival_time + "\t" + (arrival_time + TimeSpan.FromMinutes(30))
                                        + "\t" + TimeSpan.FromMinutes(30) + "\t" + distance_travelled.ToString("0.00") + "\t" + arrival_day_num + "\n\n\n");
                }

                //  global overlapping trains
                demo.WriteLine("\n\n\t" + list_of_overlapping_trains.Count + " Overlapping Trains\n");
                foreach(var train in list_of_overlapping_trains)
                {
                    demo.WriteLine("\n\t\t" + train);
                }

                if (recommended_train.sum_of_occupied_to_total_tracks == -1)
                {
                    mw.WriteLine("\n\n\n\t\t\t\t\tNo Recommended Train\n\n\n");
                    demo.WriteLine("\n\n\n\t\t\t\tNo Recommended Train\n\n\n");
                }
                else
                {
                    mw.WriteLine("\n\n\n\t\t\t\t\tRecommended Train\n\n\n");
                    demo.WriteLine("\n\n\n\t\t\t\tRecommended Train\n\n\n");

                    int i = 0;
                    float distance_travelled = 0;
                    DateTime arrival_time = recommended_train.our_train.station_pairs[0].station_1_departure_time;
                    int arrival_day_num = 0;
                    String station_2_code = "";

                    mw.WriteLine("\n\n\tStation Wise Schedule\n");
                    demo.WriteLine("\n\n\tStation Wise Schedule\n");

                    foreach (var row in recommended_train.our_train.station_pairs)
                    {
                        i++;
                        if (i == 1)
                        {
                            mw.Write("\n\t" + i + "\t" + row.station_1_code + "\t");
                            demo.Write("\n\t" + i + "\t" + row.station_1_code + "\t");
                            if (row.station_1_code.Length <= 3)
                            {
                                mw.Write("\t");
                                demo.Write("\t");
                            }
                            mw.Write((row.station_1_departure_time - TimeSpan.FromMinutes(30))
                                        + "\t" + row.station_1_departure_time + "\t" + TimeSpan.FromMinutes(30)
                                        + "\t" + distance_travelled.ToString("0.00") + "\t" + (arrival_day_num + 1) + "\n");
                            demo.Write((row.station_1_departure_time - TimeSpan.FromMinutes(30))
                                        + "\t" + row.station_1_departure_time + "\t" + TimeSpan.FromMinutes(30)
                                        + "\t" + distance_travelled.ToString("0.00") + "\t" + (arrival_day_num + 1) + "\n");
                        }
                        else
                        {
                            mw.Write("\n\t" + i + "\t" + row.station_1_code + "\t");
                            demo.Write("\n\t" + i + "\t" + row.station_1_code + "\t");
                            if (row.station_1_code.Length <= 3)
                            {
                                mw.Write("\t");
                                demo.Write("\t");
                            }
                            mw.Write(arrival_time + "\t" + row.station_1_departure_time + "\t" + (row.station_1_departure_time - arrival_time)
                                        + "\t" + distance_travelled.ToString("0.00") + "\t" + arrival_day_num + "\n");
                            demo.Write(arrival_time + "\t" + row.station_1_departure_time + "\t" + (row.station_1_departure_time - arrival_time)
                                        + "\t" + distance_travelled.ToString("0.00") + "\t" + arrival_day_num + "\n");
                        }

                        distance_travelled = distance_travelled + (float)get_distance_between_neighbouring_stations(row.station_1_code, row.station_2_code);
                        arrival_time = row.station_2_arrival_time;
                        arrival_day_num = row.arrival_day_num_at_station_2;
                        station_2_code = row.station_2_code;
                    }

                    mw.Write("\n\t" + (i + 1) + "\t" + station_2_code + "\t");
                    demo.Write("\n\t" + (i + 1) + "\t" + station_2_code + "\t");
                    if (station_2_code.Length <= 3)
                    {
                        mw.Write("\t");
                        demo.Write("\t");
                    }
                    mw.Write(arrival_time + "\t" + (arrival_time + TimeSpan.FromMinutes(30))
                                        + "\t" + TimeSpan.FromMinutes(30) + "\t" + distance_travelled.ToString("0.00") + "\t" + arrival_day_num + "\n\n\n");
                    demo.Write(arrival_time + "\t" + (arrival_time + TimeSpan.FromMinutes(30))
                                        + "\t" + TimeSpan.FromMinutes(30) + "\t" + distance_travelled.ToString("0.00") + "\t" + arrival_day_num + "\n\n\n");
                }
            }
            mw.Flush();
            demo.Flush();
            conn.Close();

            return possible_trains;
        }

        //  function to arrange and depart train at the source station which further calls a recursive function to find entire path 
        public pair_num_trains_in_same_direction_and_train_table find_path_after_source_station(DateTime current_time, TimeSpan maximum_duration_of_journey,
                                                            String current_station_code, Char[] days_of_departure_fom_source, List<String> list_of_stations,
                                                            String[] intermediate_halting_stations, String[] halt_duration_at_intermediate_halting_stations,
                                                            float avg_speed, double total_journey_length, TimeSpan time_to_complete_journey,
                                                            DateTime src_station_departure_time, int train_type)
        {
            //  find first station to visit after the source station
            String next_station_code = list_of_stations[1];

            //  find a tentative departure time (actualy departure time would be "departure_time" + 5 minutes)
            DateTime departure_time = current_time.Add(TimeSpan.FromMinutes(25));

            //  function returns train nos having overlapping departure times at given 
            //  station on given days between "departure_time" and "departure_time" + 10 minutes
            List<String> overlapping_trains = find_overlapping_departures(departure_time, days_of_departure_fom_source, current_station_code);

            //  list of strings to store train numbers of train from overlapping_trains who are going towards same direction
            List<String> overlapping_same_direction_trains = new List<String>();

            //  "our_train" stores train itinerary
            train our_train = new train();
            our_train.station_pairs = new List<row>();

            SqlConnection conn = new SqlConnection(conn_String);
            conn.Open();
            if (conn.State == System.Data.ConnectionState.Open)
            {
                foreach (var train in overlapping_trains)
                {
                    //  find distance travelled by our train at source station
                    string sql_query_1 = "select DISTANCE_TRAVELLED from USES where STATION_CODE = '" + current_station_code
                                      + "'AND TRAIN_NO = '" + train + "'";
                    SqlCommand cmd_1 = new SqlCommand(sql_query_1, conn);
                    double station_1_distance = (double)cmd_1.ExecuteScalar();

                    //  find distance travelled by our train at first station after source station
                    string sql_query_2 = "select DISTANCE_TRAVELLED from USES where STATION_CODE = '" + next_station_code
                                      + "'AND TRAIN_NO = '" + train + "'";
                    SqlCommand cmd_2 = new SqlCommand(sql_query_2, conn);

                    //  we are using try-catch block because above query might reuturn NULL 
                    //  if the "train" does not visit the station with "next_station_code"
                    try
                    {
                        double station_2_distance = (double)cmd_2.ExecuteScalar();

                        //  this shows station_2 comes after station_1 in the journey for our "train"
                        if (station_2_distance > station_1_distance)
                            overlapping_same_direction_trains.Add(train);
                    }
                    catch (Exception e)
                    {

                    }
                }

                foreach (var train in overlapping_same_direction_trains)
                {
                    Console.WriteLine("\n\t\t\tCannot Depart due to overlapping train no\t: " + train);
                }

                //  if no train is going towards our next station in this window
                if (overlapping_same_direction_trains.Count() == 0)
                {
                    //  compute departure time from source station, current_time 
                    //  is the time at which our train was brought at the platform
                    departure_time = current_time.Add(TimeSpan.FromMinutes(30));

                    //  train numbers of train whose overtaking is complete
                    List<String> train_overtaken_numbers = new List<String>();

                    //  train numbers of train who were overlapping at previous station
                    List<String> train_to_overtake_numbers = new List<string>();

                    //  summation of occupied to total tracks at each station along the path
                    float temp_sum_of_occupied_to_total_tracks = 0;
                    
                    if (list_of_stations[0].Length <= 3)
                        mw.WriteLine("\n\t\t\t\t\t\t\t" + list_of_stations[0] + "\t\t" + (departure_time - TimeSpan.FromMinutes(30))
                                                    + "\t\t" + departure_time + "\t\t" + get_no_of_platforms(list_of_stations[0]));
                    else
                        mw.WriteLine("\n\t\t\t\t\t\t\t" + list_of_stations[0] + "\t" + (departure_time - TimeSpan.FromMinutes(30))
                                                    + "\t\t" + departure_time + "\t\t" + get_no_of_platforms(list_of_stations[0]));

                    //  composite object to fetch itinerary of our train, last station 
                    //  in our itineary and arrival time at destination
                    path_complete last_station_and_arrival_time_and_our_train = recurse_intermediate_stations(departure_time, days_of_departure_fom_source,
                                                                                            1, 0, list_of_stations, intermediate_halting_stations,
                                                                                            halt_duration_at_intermediate_halting_stations, avg_speed,
                                                                                            our_train, train_overtaken_numbers,  
                                                                                            temp_sum_of_occupied_to_total_tracks, total_journey_length, 
                                                                                            0, time_to_complete_journey, src_station_departure_time, train_type);

                    mw.WriteLine("\n\n\t\t\tThreshold Journey Time  :  " + maximum_duration_of_journey
                                + "\n\t\t\tActual Journey Time    :  " + (last_station_and_arrival_time_and_our_train.arrival_time_at_destination
                                                                                    - departure_time));

                    //  check if last station of itinerary of our train is the required 
                    //  destinaton station and our train reaches destination is time
                    if (last_station_and_arrival_time_and_our_train.destination_station_code == list_of_stations[list_of_stations.Count() - 1] &&
                        last_station_and_arrival_time_and_our_train.arrival_time_at_destination - departure_time <= maximum_duration_of_journey)
                    {
                        int i = 0;
                        float distance_travelled = 0;
                        DateTime arrival_time = last_station_and_arrival_time_and_our_train.our_train.station_pairs[0].station_1_departure_time;
                        int arrival_day_num = 0;
                        String station_2_code = "";

                        mw.WriteLine("\n\n\tStation Wise Schedule\n");

                        foreach (var row in last_station_and_arrival_time_and_our_train.our_train.station_pairs)
                        {
                            i++;
                            if(i == 1)
                            {
                                mw.Write("\n\t" + i + "\t" + row.station_1_code + "\t");
                                if (row.station_1_code.Length <= 3)
                                    mw.Write("\t");
                                mw.Write((row.station_1_departure_time - TimeSpan.FromMinutes(30))
                                            + "\t" + row.station_1_departure_time + "\t" + TimeSpan.FromMinutes(30)
                                            + "\t" + distance_travelled.ToString("0.00") + "\t" + (arrival_day_num + 1) + "\n");
                            }
                            else
                            {
                                mw.Write("\n\t" + i + "\t" + row.station_1_code + "\t");
                                if (row.station_1_code.Length <= 3)
                                    mw.Write("\t");
                                mw.Write(arrival_time + "\t" + row.station_1_departure_time + "\t" + (row.station_1_departure_time - arrival_time)
                                            + "\t" + distance_travelled.ToString("0.00") + "\t" + arrival_day_num + "\n");
                            }

                            distance_travelled = distance_travelled + (float)get_distance_between_neighbouring_stations(row.station_1_code, row.station_2_code);
                            arrival_time = row.station_2_arrival_time;
                            arrival_day_num = row.arrival_day_num_at_station_2;
                            station_2_code = row.station_2_code;
                        }

                        mw.Write("\n\t" + (i + 1) + "\t" + station_2_code + "\t");
                        if (station_2_code.Length <= 3)
                            mw.Write("\t");
                        mw.Write(arrival_time + "\t" + (arrival_time + TimeSpan.FromMinutes(30)) 
                                            + "\t" + TimeSpan.FromMinutes(30) + "\t" + distance_travelled.ToString("0.00") + "\t" + arrival_day_num + "\n\n\n");

                        //  we have created a composite object to return our train itinerary and 
                        //  number of trains that are departing from the source station towards
                        //  the direction same as that of our train
                        //  -1 as number of trains in same direction is a flag that our_train is genuine
                        pair_num_trains_in_same_direction_and_train_table temp = new pair_num_trains_in_same_direction_and_train_table();
                        temp.num_trains_in_same_direction = -1;
                        temp.our_train = last_station_and_arrival_time_and_our_train.our_train;
                        temp.sum_of_occupied_to_total_tracks = last_station_and_arrival_time_and_our_train.sum_of_occupied_to_total_tracks;
                        return temp;
                    }
                }
            }
            conn.Close();

            //  we have created a composite object to return our train itinerary and 
            //  number of trains that are departing from the source station towards
            //  the direction same as that of our train
            //  Here, our_train is a dummy
            pair_num_trains_in_same_direction_and_train_table a = new pair_num_trains_in_same_direction_and_train_table();
            a.num_trains_in_same_direction = overlapping_same_direction_trains.Count();
            a.our_train = new train();
            return a;
        }

        //  a recursive function that finds path for the given parameteres 
        //  while recursing from one station to another along the route.
        //  "previous_station_num" is the index of the previous station in 
        //  "list_of_stations". So it starts from 0. 
        //  It returns a composite object of type "path_complete" whose 
        //  attributes are used back in the calling code in multiple ways.
        public path_complete recurse_intermediate_stations(DateTime departure_time_at_previous_station, char[] days_of_departure_from_source,
                                                    int departure_day_num_at_previous_station, int previous_station_num,
                                                    List<string> list_of_stations, String[] intermediate_halting_stations,
                                                    String[] halt_duration_at_intermediate_halting_stations, float avg_speed,
                                                    train our_train, List<String> train_overtaken_numbers, 
                                                    float sum_of_occupied_to_total_tracks, double total_journey_length, 
                                                    double journey_length_completed, TimeSpan time_to_complete_journey, 
                                                    DateTime source_station_departure_time, int train_type)
        {
            //  find index number of the current station
            int current_station_num = previous_station_num + 1;

            //  find a tentative arrival time for our train at the current station
            double distance = get_distance_between_neighbouring_stations(list_of_stations[previous_station_num], list_of_stations[current_station_num]);
            double avg_speed_in_kilometre_per_minute = ((double)(avg_speed)) / 60;
            TimeSpan interstation_time = TimeSpan.FromMinutes(distance / avg_speed_in_kilometre_per_minute);
            DateTime tentative_arrival_time_at_current_station = departure_time_at_previous_station + interstation_time;

            //  update the total journey length completed
            journey_length_completed = journey_length_completed + distance;

            double remaining_journey_length = total_journey_length - journey_length_completed;
            
            //  index of the current station in intermediate halting stations list (-1 if its not a halting station) 
            int intermediate_halting_station_num = is_halting_station(current_station_num, list_of_stations, intermediate_halting_stations);

            if (intermediate_halting_station_num == -1)                 //  if train does not halt at the current station
            {
                //  find a suitable window for our train to pass through the current station
                //  if a window cannot be found in 10 successive trials => too much congestion. So return.
                for (int i = 0; i < 10; i++)
                {
                    //  actual arrival time of our train for the ith trial at the current station
                    DateTime arrival_time_at_current_station = tentative_arrival_time_at_current_station + TimeSpan.FromMinutes(i);

                    //  find if enough time is left to reach the destination
                    TimeSpan time_elapsed = arrival_time_at_current_station - source_station_departure_time;
                    TimeSpan remaining_time = time_to_complete_journey - time_elapsed;

                    double maximum_coverable_distance = 0;
                    for(TimeSpan f = TimeSpan.FromMinutes(0); f < remaining_time; f = f.Add(TimeSpan.FromMinutes(1)))
                    {
                        maximum_coverable_distance = maximum_coverable_distance + avg_speed_in_kilometre_per_minute;
                    }

                    if(maximum_coverable_distance < remaining_journey_length)
                    {
                        mw.WriteLine("\t\t\t\t\tNot Enough Time Left to Reach Destination");
                        //  "break" causes the recursion to halt, hence no solution
                        break;
                    }

                    //  arrival day number of our train for the ith trial at the current station
                    int arrival_day_num_at_current_station = get_arrival_day_num_at_current_station(departure_time_at_previous_station,
                                                                        arrival_time_at_current_station, departure_day_num_at_previous_station);

                    //  days of arrival of our train for the ith trial at the current station
                    char[] days_of_arrival = find_days_of_arrival(days_of_departure_from_source, arrival_day_num_at_current_station);

                    //  period of observation is 20 minutes. Starts 10 minutes prior to arrival of
                    //  our train and ends 10 minutes after departure of our train
                    String begin_time = (arrival_time_at_current_station - TimeSpan.FromMinutes(10)).ToString("HH:mm", CultureInfo.CurrentCulture);
                    String finish_time = (arrival_time_at_current_station + TimeSpan.FromMinutes(10)).ToString("HH:mm", CultureInfo.CurrentCulture);

                    //  overlapping trains irrespective of whether they halt or not at the current station
                    List<String> overlapping_trains = get_list_of_trains_overlapping_on_concerned_days_and_time_range(days_of_arrival,
                                                        begin_time, finish_time, list_of_stations[current_station_num]);

                    //  overlapping trains that are moving in same direction as ours
                    List<String> overlapping_trains_that_move_in_same_direction = get_list_of_overlapping_trains_that_move_in_same_direction(overlapping_trains,
                                                                            list_of_stations, current_station_num);

                    //  overlapping trains that are moving in same direction as ours on the day with maximum overlap in the week
                    List<String> overlapping_trains_that_move_in_same_direction_on_day_of_maximum_overlap =
                                                                           get_train_no_on_day_of_maximum_overlaps(overlapping_trains_that_move_in_same_direction,
                                                                            list_of_stations[current_station_num], days_of_arrival);
                    
                    //  add to global list of overlapping trains
                    foreach(var train in overlapping_trains_that_move_in_same_direction_on_day_of_maximum_overlap)
                    {
                        if (list_of_overlapping_trains.Contains(train) == false)
                            list_of_overlapping_trains.Add(train);
                    }
                    
                    //  fetch number of overlapping trains on the day
                    //  with maximum number of overlappings
                    int maximum_no_of_overlaps = get_maximum_no_of_overlaps(overlapping_trains, list_of_stations[current_station_num]);

                    //  no of platforms and through track at the current station
                    int no_of_platforms = get_no_of_platforms(list_of_stations[current_station_num]);
                    int no_of_through_tracks = 2;

                    Console.WriteLine("\n\t" + list_of_stations[previous_station_num] + "\t" + departure_time_at_previous_station + "\t" + interstation_time);
                    Console.WriteLine("\n\t" + list_of_stations[current_station_num] + "\t" + arrival_time_at_current_station);

                    foreach (var train_no in overlapping_trains)
                    {
                        Console.WriteLine("\n\t" + train_no);
                    }

                    //  check if there is a track available for our train.
                    //  as our train just passes through (it will not matter
                    //  if that track is a platform or not)
                    if (maximum_no_of_overlaps < no_of_platforms + no_of_through_tracks)
                    {
                        //  update the summation of no of occupied tracks to total no of tracks ratio
                        sum_of_occupied_to_total_tracks = sum_of_occupied_to_total_tracks
                                                            + (float)((float)(maximum_no_of_overlaps) / (float)(no_of_platforms + no_of_through_tracks));

                        //  check if current station is a halt (eg. Sarvodaya Halt)
                        Boolean is_current_station_a_halt = check_if_current_station_is_a_halt(list_of_stations[current_station_num]);

                        //  manage overlapping of trains
                        mw.WriteLine("\n\n\t\t\t\t\t" + overlapping_trains_that_move_in_same_direction_on_day_of_maximum_overlap.Count + "\t" + "Overlapping Train ");

                        if (overlapping_trains_that_move_in_same_direction_on_day_of_maximum_overlap.Count() == 0)
                        {
                            //  No Issues
                        }
                        else if (overlapping_trains_that_move_in_same_direction_on_day_of_maximum_overlap.Count() == 1)
                        {
                            //  get train number of the overlapping train which is moving towards the same direction
                            String train_number = overlapping_trains_that_move_in_same_direction_on_day_of_maximum_overlap[0];
                            mw.WriteLine("\t\t\t\t\t" + train_number
                                             + "\t" + get_time(get_arrival_time_of_train_at_station(train_number, list_of_stations[current_station_num]))
                                             + "\t" + get_time(get_departure_time_of_train_at_station(train_number, list_of_stations[current_station_num]))
                                             + "\t" + get_train_type(train_number));

                            Boolean train_overtake_already_happened = false;
                            if (train_overtaken_numbers.Contains(train_number))
                            {
                                train_overtake_already_happened = true;
                                mw.WriteLine("\t\t\t\t\tOvertake Already Happened " + train_number);
                            }

                            if (train_overtake_already_happened == false)
                            {
                                //  details of the other train
                                Double speed_of_train = get_speed_of_train(train_number);
                                int type_of_train = get_train_type(train_number);

                                //  if the overlapping train did not visit previous station
                                if (get_arrival_time_of_train_at_station(train_number, list_of_stations[previous_station_num]).Equals(""))
                                    goto label;

                                DateTime arrival_time_previous_station =
                                                get_time(get_arrival_time_of_train_at_station(train_number, list_of_stations[previous_station_num]));
                                DateTime departure_time_previous_station =
                                                get_time(get_departure_time_of_train_at_station(train_number, list_of_stations[previous_station_num]));
                                
                                DateTime arrival_time_current_station =
                                                get_time(get_arrival_time_of_train_at_station(train_number, list_of_stations[current_station_num]));
                                DateTime departure_time_current_station =
                                                get_time(get_departure_time_of_train_at_station(train_number, list_of_stations[current_station_num]));

                                //  if our train left the previous station first
                                if (departure_time_at_previous_station < departure_time_previous_station)
                                {
                                    //  if our train has higher speed
                                    if (avg_speed >= speed_of_train)
                                    {
                                        //  No Issues
                                        mw.WriteLine("\t\t\t\t\tOur train with higher speed left" + "\t" + list_of_stations[previous_station_num] +
                                                                "\t" + "before" + "\t" + train_number);
                                    }
                                    else
                                    {
                                        //  if our train arrives at current station after the other train
                                        if (arrival_time_current_station < arrival_time_at_current_station)
                                        {
                                            mw.WriteLine("\t\t\t\t\tOur train left" + "\t" + list_of_stations[previous_station_num] +
                                                                "\t" + "first but Arrived at" + "\t" + list_of_stations[current_station_num] +
                                                                "\t" + "after " + train_number);
                                            //  "break" causes the recursion to halt, hence no solution
                                            break;
                                        }
                                        //  if our train arrives at current station before the other train
                                        else
                                        {
                                            //  halt time of overlapping train should be low (allowing a  window of 2 minutes)
                                            if (((departure_time_current_station - arrival_time_current_station) <= TimeSpan.FromMinutes(2))
                                                    && (is_current_station_a_halt == false))
                                            {
                                                //  if overlapping train has a higher priority
                                                if(type_of_train < train_type)
                                                {
                                                    if (arrival_time_current_station - arrival_time_at_current_station <= TimeSpan.FromMinutes(5))
                                                    {
                                                        mw.WriteLine("\t\t\t\t\tOvertaken by " + train_number);
                                                        train_overtaken_numbers.Add(train_number);
                                                    }
                                                    else
                                                    {
                                                        mw.WriteLine("\t\t\t\t\tTime Gap more than 5 minute " + train_number + "\tat\t" + arrival_time_at_current_station);
                                                    }
                                                }
                                                else
                                                {
                                                    mw.WriteLine("\t\t\t\t\t" + train_number + " does not have higher priority than our train");
                                                }
                                            }
                                            else
                                            {
                                                mw.WriteLine("\t\t\t\t\tOur train left" + "\t" + list_of_stations[previous_station_num] +
                                                                "\t" + "first and Arrived at" + "\t" + list_of_stations[current_station_num] +
                                                                "\t" + "before " + train_number);
                                            }
                                        }
                                    }
                                }
                                //  if our train left the previous station second
                                else
                                {
                                    //  if our train has lower speed
                                    if (avg_speed <= speed_of_train)
                                    {
                                        //  No Issues
                                        mw.WriteLine("\t\t\t\t\tOur train with lower speed left" + "\t" + list_of_stations[previous_station_num] +
                                                                "\t" + "after" + "\t" + train_number);
                                    }
                                    else
                                    {
                                        //  if the overlapping train halts at current station and current station is not a halt,
                                        //  then only our train can overtake this overlapping train
                                        if ((departure_time_current_station > arrival_time_current_station) && (is_current_station_a_halt == false))
                                        {
                                            //  if overlapping train has a lower priority or Long Halting
                                            if (type_of_train > train_type || 
                                                departure_time_current_station - arrival_time_current_station >= TimeSpan.FromMinutes(20))
                                            {
                                                //  if the overlapping train arrives before our train
                                                if (arrival_time_at_current_station >= arrival_time_current_station)
                                                {
                                                    if (arrival_time_at_current_station - arrival_time_current_station <= TimeSpan.FromMinutes(5))
                                                    {
                                                        mw.WriteLine("\t\t\t\t\tOvertakes " + train_number);
                                                        train_overtaken_numbers.Add(train_number);
                                                    }
                                                    else
                                                    {
                                                        mw.WriteLine("\t\t\t\t\tTime Gap more than 5 minute " + train_number + "\tat\t" + arrival_time_at_current_station);
                                                    }
                                                }
                                                //  if the overlapping train arrives after our train
                                                else
                                                {
                                                    //  slow our train
                                                    mw.WriteLine("\t\t\t\t\tDelay arrival time by another minute (overtake) " + list_of_stations[current_station_num]
                                                                            + "\tat\t" + arrival_time_at_current_station);
                                                    //  "continue" causes the next iteration with incremented value of 
                                                    //  "arrival_time_at_current_station" so that an overtake can happen
                                                    continue;
                                                }
                                            }
                                            else
                                            {
                                                mw.WriteLine("\t\t\t\t\t" + train_number + " has higher priority than our train");
                                            }
                                        }
                                        else
                                        {
                                            if (arrival_time_at_current_station <= arrival_time_current_station + TimeSpan.FromMinutes(2))
                                            {
                                                mw.WriteLine("\t\t\t\t\tDelay arrival time by another minute (no overtake) " + list_of_stations[current_station_num]
                                                                        + "\tat\t" + arrival_time_at_current_station);
                                                //  "continue" causes the next iteration with incremented value of 
                                                //  "arrival_time_at_current_station" so that our train departs after other train
                                                continue;
                                            }
                                            else
                                            {
                                                mw.WriteLine("\t\t\t\t\tTrains sufficiently apart" + "\tat\t" + arrival_time_at_current_station);
                                            }
                                        }
                                    }
                                }

                                label:
                                //  if the overlapping train did not visit previous station

                                arrival_time_current_station =
                                                get_time(get_arrival_time_of_train_at_station(train_number, list_of_stations[current_station_num]));
                                departure_time_current_station =
                                                get_time(get_departure_time_of_train_at_station(train_number, list_of_stations[current_station_num]));

                                //  halt time of overlapping train should be low (allowing a  window of 2 minutes)
                                if (((departure_time_current_station - arrival_time_current_station) <= TimeSpan.FromMinutes(2))
                                        && (is_current_station_a_halt == false) && avg_speed < speed_of_train)
                                {
                                    //  if overlapping train has a higher priority
                                    if (type_of_train < train_type)
                                    {
                                        if (arrival_time_current_station - arrival_time_at_current_station <= TimeSpan.FromMinutes(5))
                                        {
                                            mw.WriteLine("\t\t\t\t\tOvertaken by " + train_number);
                                            train_overtaken_numbers.Add(train_number);
                                        }
                                        else
                                        {
                                            mw.WriteLine("\t\t\t\t\tTime Gap more than 5 minute " + train_number + "\tat\t" + arrival_time_at_current_station);
                                        }
                                    }
                                    else
                                    {
                                        mw.WriteLine("\t\t\t\t\t" + train_number + " does not have higher priority than our train");
                                    }
                                }

                                //  if the overlapping train halts at current station and current station is not a halt,
                                //  then only our train can overtake this overlapping train
                                if ((departure_time_current_station > arrival_time_current_station) && 
                                        (is_current_station_a_halt == false) && avg_speed > speed_of_train)
                                {
                                    //  if overlapping train has a lower priority or Long Halting
                                    if (type_of_train > train_type ||
                                        departure_time_current_station - arrival_time_current_station >= TimeSpan.FromMinutes(20))
                                    {
                                        //  if the overlapping train arrives before our train
                                        if (arrival_time_at_current_station >= arrival_time_current_station)
                                        {
                                            if (arrival_time_at_current_station - arrival_time_current_station <= TimeSpan.FromMinutes(5))
                                            {
                                                mw.WriteLine("\t\t\t\t\tOvertakes " + train_number);
                                                train_overtaken_numbers.Add(train_number);
                                            }
                                            else
                                            {
                                                mw.WriteLine("\t\t\t\t\tTime Gap more than 5 minute " + train_number + "\tat\t" + arrival_time_at_current_station);
                                            }
                                        }
                                        //  if the overlapping train arrives after our train
                                        else
                                        {
                                            //  slow our train
                                            mw.WriteLine("\t\t\t\t\tDelay arrival time by another minute (overtake) " + list_of_stations[current_station_num]
                                                                    + "\tat\t" + arrival_time_at_current_station);
                                            //  "continue" causes the next iteration with incremented value of 
                                            //  "arrival_time_at_current_station" so that an overtake can happen
                                            continue;
                                        }
                                    }
                                    else
                                    {
                                        mw.WriteLine("\t\t\t\t\t" + train_number + " has higher priority than our train");
                                    }
                                }
                            }
                        }
                        else if(overlapping_trains_that_move_in_same_direction_on_day_of_maximum_overlap.Count() == 2)
                        {
                            //  get train number of the 2 overlapping trains which are moving towards the same direction
                            String train_1_number = overlapping_trains_that_move_in_same_direction_on_day_of_maximum_overlap[0];
                            String train_2_number = overlapping_trains_that_move_in_same_direction_on_day_of_maximum_overlap[1];

                            DateTime train_1_arrival_time = get_time(get_arrival_time_of_train_at_station(train_1_number, list_of_stations[current_station_num]));
                            DateTime train_1_departure_time = get_time(get_departure_time_of_train_at_station(train_1_number, list_of_stations[current_station_num]));
                            TimeSpan train_1_halt_duration = train_1_departure_time - train_1_arrival_time;
                            Double train_1_speed = get_speed_of_train(train_1_number);
                            int type_of_train_1 = get_train_type(train_1_number);

                            DateTime train_2_arrival_time = get_time(get_arrival_time_of_train_at_station(train_2_number, list_of_stations[current_station_num]));
                            DateTime train_2_departure_time = get_time(get_departure_time_of_train_at_station(train_2_number, list_of_stations[current_station_num]));
                            TimeSpan train_2_halt_duration = train_2_departure_time - train_2_arrival_time;
                            Double train_2_speed = get_speed_of_train(train_2_number);
                            int type_of_train_2 = get_train_type(train_2_number);

                            mw.WriteLine("\t\t\t\t\t" + train_1_number + "\t" + train_1_arrival_time + "\t" + train_1_departure_time + "\t" + type_of_train_1);
                            mw.WriteLine("\t\t\t\t\t" + train_2_number + "\t" + train_2_arrival_time + "\t" + train_2_departure_time + "\t" + type_of_train_2);

                            int no_of_overtakes = 0;

                            TimeSpan max_halt_duration = train_1_halt_duration;
                            if (train_2_halt_duration > train_1_halt_duration)
                                max_halt_duration = train_2_halt_duration;

                            if(max_halt_duration < TimeSpan.FromMinutes(10))
                            {
                                mw.WriteLine("\t\t\t\t\tNot Enough Halt Duration");
                                mw.WriteLine("\t\t\t\t\t" + train_1_number + "\t" + train_1_halt_duration);
                                mw.WriteLine("\t\t\t\t\t" + train_2_number + "\t" + train_2_halt_duration);
                                //  "break" causes the recursion to halt, hence no solution
                                break;
                            }

                            Boolean train_1_already_overtaken = false;
                            Boolean train_2_already_overtaken = false;

                            if (train_overtaken_numbers.Contains(train_1_number))
                            {
                                train_1_already_overtaken = true;
                                mw.WriteLine("\t\t\t\t\t" + train_1_number + "\tOvertake Already Happened ");
                                no_of_overtakes = no_of_overtakes + 1;
                            }

                            if (train_overtaken_numbers.Contains(train_2_number))
                            {
                                train_2_already_overtaken = true;
                                mw.WriteLine("\t\t\t\t\t" + train_2_number + "\tOvertake Already Happened ");
                                no_of_overtakes = no_of_overtakes + 1;
                            }

                            if (train_1_already_overtaken == false)
                            {
                                if ((train_1_halt_duration >= TimeSpan.FromMinutes(20)
                                && arrival_time_at_current_station >= train_1_arrival_time - TimeSpan.FromMinutes(5)
                                && arrival_time_at_current_station <= train_1_departure_time + TimeSpan.FromMinutes(5)
                                && avg_speed > train_1_speed)
                                || (train_1_halt_duration >= TimeSpan.FromMinutes(10) 
                                && train_1_halt_duration < TimeSpan.FromMinutes(20)
                                && arrival_time_at_current_station >= train_1_arrival_time - TimeSpan.FromMinutes(2)
                                && arrival_time_at_current_station <= train_1_departure_time + TimeSpan.FromMinutes(2)
                                && avg_speed > train_1_speed
                                && type_of_train_1 > train_type))
                                {
                                    mw.WriteLine("\t\t\t\t\tOvertakes " + train_1_number);
                                    train_overtaken_numbers.Add(train_1_number);
                                    no_of_overtakes = no_of_overtakes + 1;
                                }
                            }

                            if (train_2_already_overtaken == false)
                            {
                                if ((train_2_halt_duration >= TimeSpan.FromMinutes(20)
                                && arrival_time_at_current_station >= train_2_arrival_time - TimeSpan.FromMinutes(5)
                                && arrival_time_at_current_station <= train_2_departure_time + TimeSpan.FromMinutes(5)
                                && avg_speed > train_2_speed)
                                || (train_2_halt_duration >= TimeSpan.FromMinutes(10) 
                                && train_2_halt_duration < TimeSpan.FromMinutes(20)
                                && arrival_time_at_current_station >= train_2_arrival_time - TimeSpan.FromMinutes(2)
                                && arrival_time_at_current_station <= train_2_departure_time + TimeSpan.FromMinutes(2)
                                && avg_speed > train_2_speed
                                && type_of_train_2 > train_type))
                                {
                                    mw.WriteLine("\t\t\t\t\tOvertakes " + train_2_number);
                                    train_overtaken_numbers.Add(train_2_number);
                                    no_of_overtakes = no_of_overtakes + 1;
                                }
                            }

                            if(no_of_overtakes == 0)
                            {
                                mw.WriteLine("\t\t\t\t\tNo Overtake Possible");
                                //  "break" causes the recursion to halt, hence no solution
                                break;
                            }
                        }
                        else
                        {
                            mw.WriteLine("\t\t\t\t\t3 or More Trains");
                            //  "break" causes the recursion to halt, hence no solution
                            break;
                        }

                        //  print statements for the purpose of debugging
                        Console.WriteLine("\n\t\t\t\t\t\t\t" + list_of_stations[previous_station_num] + "\t" + list_of_stations[current_station_num]
                                                    + "\t" + departure_time_at_previous_station + "\t" + arrival_time_at_current_station
                                                    + "\t" + no_of_platforms + "\t" + no_of_through_tracks + "\t" + "HALT");
                        if (list_of_stations[current_station_num].Length <= 3)
                            mw.Write("\n\t\t\t\t\t\t\t" + list_of_stations[current_station_num] + "\t\t" + arrival_time_at_current_station
                                                    + "\t\t" + arrival_time_at_current_station + "\t\t" + no_of_platforms);
                        else
                            mw.Write("\n\t\t\t\t\t\t\t" + list_of_stations[current_station_num] + "\t" + arrival_time_at_current_station
                                                    + "\t\t" + arrival_time_at_current_station + "\t\t" + no_of_platforms);

                        if (is_current_station_a_halt == true)
                            mw.WriteLine("\t" + "HALT");
    
                        //  form a station pair of neighbouring stations along the route
                        row station_pair = get_row_element(list_of_stations[previous_station_num], list_of_stations[current_station_num],
                                                            departure_time_at_previous_station, arrival_time_at_current_station,
                                                            arrival_day_num_at_current_station, no_of_platforms);

                        //  add this station pair to the "station_pairs" of our train
                        our_train.station_pairs.Add(station_pair);

                        if (current_station_num != list_of_stations.Count() - 1)        //  if current station is not the destination station
                            return recurse_intermediate_stations(arrival_time_at_current_station, days_of_departure_from_source, arrival_day_num_at_current_station,
                                                            current_station_num, list_of_stations, intermediate_halting_stations,
                                                            halt_duration_at_intermediate_halting_stations, avg_speed, our_train, train_overtaken_numbers,
                                                            sum_of_occupied_to_total_tracks, total_journey_length, journey_length_completed,
                                                            time_to_complete_journey, source_station_departure_time, train_type);
                        else                                                            //  if current station is the destination station
                        {
                            path_complete temp = new path_complete();                              //  we are making this composite object because  
                            temp.arrival_time_at_destination = arrival_time_at_current_station;    //  we needed to return arrival time at destination
                            temp.destination_station_code = list_of_stations[current_station_num]; //  as well as destination station code to check if
                            temp.our_train = our_train;                                            //  we reached in time to our destination or not
                            temp.sum_of_occupied_to_total_tracks = sum_of_occupied_to_total_tracks;
                            return temp;
                        }
                    }
                    else
                    {
                        mw.WriteLine("\n\t\t\t\t\tToo many trains at\t" + list_of_stations[current_station_num] + "\tat\t" + arrival_time_at_current_station);
                    }
                }
            }
            else                                                    //  if train halts at the current station
            {
                //  find a suitable window for the train to halt for the required time at  the current station
                //  if a window cannot be found in successive trials => too much congestion. So return.
                for (int i = 0; i < 10; i++)
                {
                    //  find halt duration of the train at the current station
                    TimeSpan halt_duration = TimeSpan.Parse(halt_duration_at_intermediate_halting_stations[intermediate_halting_station_num]);

                    //  actual arrival and departure time of our train for the ith trial at the current station
                    DateTime arrival_time_at_current_station = tentative_arrival_time_at_current_station + TimeSpan.FromMinutes(i);
                    DateTime departure_time_at_current_station = arrival_time_at_current_station + halt_duration;

                    //  find if enough time is left to reach the destination
                    TimeSpan time_elapsed = arrival_time_at_current_station - source_station_departure_time;
                    TimeSpan remaining_time = time_to_complete_journey - time_elapsed;

                    double maximum_coverable_distance = 0;
                    for (TimeSpan f = TimeSpan.FromMinutes(0); f < remaining_time; f = f.Add(TimeSpan.FromMinutes(1)))
                    {
                        maximum_coverable_distance = maximum_coverable_distance + avg_speed_in_kilometre_per_minute;
                    }

                    if (maximum_coverable_distance < remaining_journey_length)
                    {
                        mw.WriteLine("\t\t\t\t\tNot Enough Time Left to Reach Destination");
                        //  "break" causes the recursion to halt, hence no solution
                        break;
                    }

                    //  arrival day number of our train for the ith trial at the current station
                    int arrival_day_num_at_current_station = get_arrival_day_num_at_current_station(departure_time_at_previous_station,
                                                                        arrival_time_at_current_station, departure_day_num_at_previous_station);
                    int departure_day_num_at_current_station = get_arrival_day_num_at_current_station(departure_time_at_previous_station,
                                                                        departure_time_at_current_station, departure_day_num_at_previous_station);

                    //  week days of arrival of our train for the ith trial at the current station
                    char[] days_of_arrival = find_days_of_arrival(days_of_departure_from_source, arrival_day_num_at_current_station);

                    //  period of observation is 10 + halt_duration + 10 minutes. Starts 10 minutes 
                    //  prior to arrival of our train and ends 10 minutes after departure of our train.  
                    String begin_time = (arrival_time_at_current_station - TimeSpan.FromMinutes(10)).ToString("HH:mm", CultureInfo.CurrentCulture);
                    String finish_time = (departure_time_at_current_station + TimeSpan.FromMinutes(10)).ToString("HH:mm", CultureInfo.CurrentCulture);

                    //  overlapping trains irrespective of whether they halt or not at the current station
                    List<String> overlapping_trains = get_list_of_trains_overlapping_on_concerned_days_and_time_range(days_of_arrival,
                                                        begin_time, finish_time, list_of_stations[current_station_num]);

                    //  overlapping trains that are moving in same direction as ours
                    List<String> overlapping_trains_that_move_in_same_direction = get_list_of_overlapping_trains_that_move_in_same_direction(overlapping_trains,
                                                                            list_of_stations, current_station_num);

                    //  overlapping trains that are moving in same direction as ours on the day with maximum overlap in the week
                    List<String> overlapping_trains_that_move_in_same_direction_on_day_of_maximum_overlap =
                                                                           get_train_no_on_day_of_maximum_overlaps(overlapping_trains_that_move_in_same_direction,
                                                                            list_of_stations[current_station_num], days_of_arrival);

                    //  add to global list of overlapping trains
                    foreach (var train in overlapping_trains_that_move_in_same_direction_on_day_of_maximum_overlap)
                    {
                        if (list_of_overlapping_trains.Contains(train) == false)
                            list_of_overlapping_trains.Add(train);
                    }

                    //  fetch number of overlapping trains on the day
                    //  with maximum number of overlappings
                    int maximum_no_of_overlaps = get_maximum_no_of_overlaps(overlapping_trains, list_of_stations[current_station_num]);

                    //  overlapping trains which halt at the current station
                    List<String> halting_overlapping_trains = find_overlapping_trains_that_halt(overlapping_trains,
                                                                                            list_of_stations[current_station_num]);

                    //  fetch number of halting overlapping trains on the day
                    //  with maximum number of overlappings
                    int maximum_no_of_overlaps_for_halting_trains = get_maximum_no_of_overlaps(halting_overlapping_trains, list_of_stations[current_station_num]);

                    //  no of platforms and through track at the current station
                    int no_of_platforms = get_no_of_platforms(list_of_stations[current_station_num]);
                    int no_of_through_tracks = 2;

                    Console.WriteLine("\n\t" + list_of_stations[previous_station_num] + "\t" + departure_time_at_previous_station + "\t" + interstation_time);
                    Console.WriteLine("\n\t" + list_of_stations[current_station_num] + "\t" + arrival_time_at_current_station);

                    foreach (var train_no in overlapping_trains)
                    {
                        Console.WriteLine("\n\t" + train_no);
                    }

                    //  check if there is a platform available for our train from arrival to departure
                    //  and also in total there is a track available from arrival to departure
                    if (maximum_no_of_overlaps_for_halting_trains < no_of_platforms &&
                        maximum_no_of_overlaps < no_of_through_tracks + no_of_platforms)
                    {
                        //  update the summation of no of occupied tracks to total no of tracks ratio
                        sum_of_occupied_to_total_tracks = sum_of_occupied_to_total_tracks
                                                            + (float)((float)(maximum_no_of_overlaps) / (float)(no_of_platforms + no_of_through_tracks));

                        //  check if current station is a halt (eg. Sarvodaya Halt)
                        Boolean is_current_station_a_halt = check_if_current_station_is_a_halt(list_of_stations[current_station_num]);

                        //  manage overlapping of trains
                        mw.WriteLine("\n\n\t\t\t\t\t" + overlapping_trains_that_move_in_same_direction_on_day_of_maximum_overlap.Count + "\t" + "Overlapping train ");
                        if (overlapping_trains_that_move_in_same_direction_on_day_of_maximum_overlap.Count() == 0)
                        {
                            //  No Issues
                        }
                        else if (overlapping_trains_that_move_in_same_direction_on_day_of_maximum_overlap.Count() == 1)
                        {
                            //  get train number of the overlapping train which is moving towards the same direction
                            String train_number = overlapping_trains_that_move_in_same_direction_on_day_of_maximum_overlap[0];
                            mw.WriteLine("\t\t\t\t\t" + train_number
                                             + "\t" + get_time(get_arrival_time_of_train_at_station(train_number, list_of_stations[current_station_num]))
                                             + "\t" + get_time(get_departure_time_of_train_at_station(train_number, list_of_stations[current_station_num]))
                                             + "\t" + get_train_type(train_number));

                            Boolean train_overtake_already_happened = false;
                            if (train_overtaken_numbers.Contains(train_number))
                            {
                                train_overtake_already_happened = true;
                                mw.WriteLine("\t\t\t\t\tOvertake Already Happened " + train_number);
                            }

                            if (train_overtake_already_happened == false)
                            {
                                //  details of the other train
                                Double speed_of_train = get_speed_of_train(train_number);
                                int type_of_train = get_train_type(train_number);

                                //  if the overlapping train did not visit previous station
                                if (get_arrival_time_of_train_at_station(train_number, list_of_stations[previous_station_num]).Equals(""))
                                    goto label_halt;

                                DateTime arrival_time_previous_station =
                                                get_time(get_arrival_time_of_train_at_station(train_number, list_of_stations[previous_station_num]));
                                DateTime departure_time_previous_station =
                                                get_time(get_departure_time_of_train_at_station(train_number, list_of_stations[previous_station_num]));

                                DateTime arrival_time_current_station =
                                                get_time(get_arrival_time_of_train_at_station(train_number, list_of_stations[current_station_num]));
                                DateTime departure_time_current_station =
                                                get_time(get_departure_time_of_train_at_station(train_number, list_of_stations[current_station_num]));

                                //  if our train left the previous station first
                                if (departure_time_at_previous_station < departure_time_previous_station)
                                {
                                    //  if our train has higher speed
                                    if (avg_speed >= speed_of_train)
                                    {
                                        //  No Issues
                                        mw.WriteLine("\t\t\t\t\tOur train with higher speed left" + "\t" + list_of_stations[previous_station_num] +
                                                                "\t" + "before" + "\t" + train_number);
                                    }
                                    else
                                    {
                                        //  if our train arrives at current station after the other train
                                        if (arrival_time_current_station < arrival_time_at_current_station)
                                        {
                                            mw.WriteLine("\t\t\t\t\tOur train left" + "\t" + list_of_stations[previous_station_num] +
                                                                "\t" + "first but Arrived at" + "\t" + list_of_stations[current_station_num] +
                                                                "\t" + "after " + train_number);
                                            //  "break" causes the recursion to halt, hence no solution
                                            break;
                                        }
                                        //  if our train arrives at current station before the other train
                                        else
                                        {
                                            //  halt time of overlapping train should be lesser (allowing a  window of 2 minutes)
                                            if(((departure_time_current_station - arrival_time_current_station) <= halt_duration + TimeSpan.FromMinutes(2))
                                                    &&  (is_current_station_a_halt == false))
                                            {
                                                //  if overlapping train has higher priority than our train
                                                if(type_of_train < train_type)
                                                {
                                                    if (arrival_time_current_station - arrival_time_at_current_station <= TimeSpan.FromMinutes(5))
                                                    {
                                                        mw.WriteLine("\t\t\t\t\tOvertaken by " + train_number);
                                                        train_overtaken_numbers.Add(train_number);
                                                    }
                                                    else
                                                    {
                                                        mw.WriteLine("\t\t\t\t\tTime Gap more than 5 minute " + train_number + "\tat\t" + arrival_time_at_current_station);
                                                    }
                                                }
                                                else
                                                {
                                                    mw.WriteLine("\t\t\t\t\t" + train_number + " does not have higher priority than our train");
                                                }
                                            }
                                            else
                                            {
                                                mw.WriteLine("\t\t\t\t\tOur train left" + "\t" + list_of_stations[previous_station_num] +
                                                                "\t" + "first and Arrived at" + "\t" + list_of_stations[current_station_num] +
                                                                "\t" + "before " + train_number);
                                            }
                                        }
                                    }
                                }
                                //  if our train left the previous station second
                                else
                                {
                                    //  if our train has lower speed
                                    if (avg_speed <= speed_of_train)
                                    {
                                        //  No Issues
                                        mw.WriteLine("\t\t\t\t\tOur train with lower speed left" + "\t" + list_of_stations[previous_station_num] +
                                                                "\t" + "after" + "\t" + train_number);
                                    }
                                    else
                                    {
                                        //  if the overlapping train halts at current station and current station is not a halt,
                                        //  then only our train can overtake this overlapping train
                                        if ((departure_time_current_station - arrival_time_current_station >= halt_duration) 
                                                && (is_current_station_a_halt == false))
                                        {
                                            //  if overlapping train has lower priority than our train or Long Halting
                                            if(type_of_train > train_type || 
                                                departure_time_current_station - arrival_time_current_station >= TimeSpan.FromMinutes(20))
                                            {
                                                //  if the overlapping trains arrives before our train
                                                if (arrival_time_at_current_station >= arrival_time_current_station)
                                                {
                                                    if (arrival_time_at_current_station - arrival_time_current_station <= TimeSpan.FromMinutes(5))
                                                    {
                                                        mw.WriteLine("\t\t\t\t\tOvertakes " + train_number);
                                                        train_overtaken_numbers.Add(train_number);
                                                    }
                                                    else
                                                    {
                                                        mw.WriteLine("\t\t\t\t\tTime Gap more than 5 minute " + train_number + "\tat\t" + arrival_time_at_current_station);
                                                    }
                                                }
                                                //  if the overlapping train arrives after our train
                                                else
                                                {
                                                    //  slow our train
                                                    mw.WriteLine("\t\t\t\t\tDelay arrival time by another minute (overtake) " + list_of_stations[current_station_num]
                                                                            + "\tat\t" + arrival_time_at_current_station);
                                                    //  "continue" causes the next iteration with incremented value of 
                                                    //  "arrival_time_at_current_station" so that an overtake can happen
                                                    continue;
                                                }
                                            }
                                            else
                                            {
                                                mw.WriteLine("\t\t\t\t\t" + train_number + " has higher priority than our train");
                                            }
                                        }
                                        else
                                        {
                                            if (arrival_time_at_current_station <= arrival_time_current_station + TimeSpan.FromMinutes(2))
                                            {
                                                mw.WriteLine("\t\t\t\t\tDelay arrival time by another minute (no overtake) " + list_of_stations[current_station_num]
                                                                        + "\tat\t" + arrival_time_at_current_station);
                                                //  "continue" causes the next iteration with incremented value of 
                                                //  "arrival_time_at_current_station" so that our train departs after other train
                                                continue;
                                            }
                                            else
                                            {
                                                mw.WriteLine("\t\t\t\t\tTrains sufficiently apart" + "\tat\t" + arrival_time_at_current_station);
                                            }
                                        }
                                    }
                                }

                                label_halt:
                                //  if the overlapping train did not visit previous station

                                arrival_time_current_station =
                                                get_time(get_arrival_time_of_train_at_station(train_number, list_of_stations[current_station_num]));
                                departure_time_current_station =
                                                get_time(get_departure_time_of_train_at_station(train_number, list_of_stations[current_station_num]));

                                //  halt time of overlapping train should be lesser (allowing a  window of 2 minutes)
                                if (((departure_time_current_station - arrival_time_current_station) <= halt_duration + TimeSpan.FromMinutes(2))
                                        && (is_current_station_a_halt == false) && avg_speed < speed_of_train)
                                {
                                    //  if overlapping train has higher priority than our train
                                    if (type_of_train < train_type)
                                    {
                                        if (arrival_time_current_station - arrival_time_at_current_station <= TimeSpan.FromMinutes(5))
                                        {
                                            mw.WriteLine("\t\t\t\t\tOvertaken by " + train_number);
                                            train_overtaken_numbers.Add(train_number);
                                        }
                                        else
                                        {
                                            mw.WriteLine("\t\t\t\t\tTime Gap more than 5 minute " + train_number + "\tat\t" + arrival_time_at_current_station);
                                        }
                                    }
                                    else
                                    {
                                        mw.WriteLine("\t\t\t\t\t" + train_number + " does not have higher priority than our train");
                                    }
                                }

                                //  if the overlapping train halts at current station and current station is not a halt,
                                //  then only our train can overtake this overlapping train
                                if ((departure_time_current_station - arrival_time_current_station >= halt_duration)
                                        && (is_current_station_a_halt == false) && avg_speed > speed_of_train)
                                {
                                    //  if overlapping train has lower priority than our train or Long Halting
                                    if (type_of_train > train_type ||
                                        departure_time_current_station - arrival_time_current_station >= TimeSpan.FromMinutes(20))
                                    {
                                        //  if the overlapping trains arrives before our train
                                        if (arrival_time_at_current_station >= arrival_time_current_station)
                                        {
                                            if (arrival_time_at_current_station - arrival_time_current_station <= TimeSpan.FromMinutes(5))
                                            {
                                                mw.WriteLine("\t\t\t\t\tOvertakes " + train_number);
                                                train_overtaken_numbers.Add(train_number);
                                            }
                                            else
                                            {
                                                mw.WriteLine("\t\t\t\t\tTime Gap more than 5 minute " + train_number + "\tat\t" + arrival_time_at_current_station);
                                            }
                                        }
                                        //  if the overlapping train arrives after our train
                                        else
                                        {
                                            //  slow our train
                                            mw.WriteLine("\t\t\t\t\tDelay arrival time by another minute (overtake) " + list_of_stations[current_station_num]
                                                                    + "\tat\t" + arrival_time_at_current_station);
                                            //  "continue" causes the next iteration with incremented value of 
                                            //  "arrival_time_at_current_station" so that an overtake can happen
                                            continue;
                                        }
                                    }
                                    else
                                    {
                                        mw.WriteLine("\t\t\t\t\t" + train_number + " has higher priority than our train");
                                    }
                                }
                            }
                        }
                        else if (overlapping_trains_that_move_in_same_direction_on_day_of_maximum_overlap.Count() == 2)
                        {
                            //  get train number of the 2 overlapping trains which are moving towards the same direction
                            String train_1_number = overlapping_trains_that_move_in_same_direction_on_day_of_maximum_overlap[0];
                            String train_2_number = overlapping_trains_that_move_in_same_direction_on_day_of_maximum_overlap[1];

                            DateTime train_1_arrival_time = get_time(get_arrival_time_of_train_at_station(train_1_number, list_of_stations[current_station_num]));
                            DateTime train_1_departure_time = get_time(get_departure_time_of_train_at_station(train_1_number, list_of_stations[current_station_num]));
                            TimeSpan train_1_halt_duration = train_1_departure_time - train_1_arrival_time;
                            Double train_1_speed = get_speed_of_train(train_1_number);
                            int type_of_train_1 = get_train_type(train_1_number);

                            DateTime train_2_arrival_time = get_time(get_arrival_time_of_train_at_station(train_2_number, list_of_stations[current_station_num]));
                            DateTime train_2_departure_time = get_time(get_departure_time_of_train_at_station(train_2_number, list_of_stations[current_station_num]));
                            TimeSpan train_2_halt_duration = train_2_departure_time - train_2_arrival_time;
                            Double train_2_speed = get_speed_of_train(train_2_number);
                            int type_of_train_2 = get_train_type(train_2_number);

                            mw.WriteLine("\t\t\t\t\t" + "\t" + train_1_arrival_time + "\t" + train_1_departure_time);
                            mw.WriteLine("\t\t\t\t\t" + train_2_number + "\t" + train_2_arrival_time + "\t" + train_2_departure_time);

                            int no_of_overtakes = 0;

                            TimeSpan max_halt_duration = train_1_halt_duration;
                            if (train_2_halt_duration > max_halt_duration)
                                max_halt_duration = train_2_halt_duration;
                            if (halt_duration > max_halt_duration)
                                max_halt_duration = halt_duration;

                            if (max_halt_duration < TimeSpan.FromMinutes(10) + halt_duration)
                            {
                                mw.WriteLine("\t\t\t\t\tNot Enough Halt Duration");
                                mw.WriteLine("\t\t\t\t\t" + train_1_number + "\t" + train_1_halt_duration);
                                mw.WriteLine("\t\t\t\t\t" + train_2_number + "\t" + train_2_halt_duration);
                                //  "break" causes the recursion to halt, hence no solution
                                break;
                            }

                            Boolean train_1_already_overtaken = false;
                            Boolean train_2_already_overtaken = false;

                            if (train_overtaken_numbers.Contains(train_1_number))
                            {
                                train_1_already_overtaken = true;
                                mw.WriteLine("\t\t\t\t\t" + train_1_number + "\tOvertake Already Happened ");
                                no_of_overtakes = no_of_overtakes + 1;
                            }

                            if (train_overtaken_numbers.Contains(train_2_number))
                            {
                                train_2_already_overtaken = true;
                                mw.WriteLine("\t\t\t\t\t" + train_2_number + "\tOvertake Already Happened ");
                                no_of_overtakes = no_of_overtakes + 1;
                            }

                            if (train_1_already_overtaken == false)
                            {
                                if(type_of_train_1 > train_type)
                                {
                                    if ((train_1_halt_duration >= TimeSpan.FromMinutes(20) + halt_duration
                                    && arrival_time_at_current_station >= train_1_arrival_time - TimeSpan.FromMinutes(5)
                                    && arrival_time_at_current_station + halt_duration <= train_1_departure_time + TimeSpan.FromMinutes(5))
                                    || (train_1_halt_duration >= TimeSpan.FromMinutes(10) + halt_duration
                                    && train_1_halt_duration < TimeSpan.FromMinutes(20) + halt_duration
                                    && arrival_time_at_current_station >= train_1_arrival_time - TimeSpan.FromMinutes(2)
                                    && arrival_time_at_current_station + halt_duration <= train_1_departure_time + TimeSpan.FromMinutes(2)))
                                    {
                                        mw.WriteLine("\t\t\t\t\tOvertakes " + train_1_number);
                                        train_overtaken_numbers.Add(train_1_number);
                                        no_of_overtakes = no_of_overtakes + 1;
                                    }
                                }
                                else
                                {
                                    if ((halt_duration >= TimeSpan.FromMinutes(20) + train_1_halt_duration
                                    && train_1_arrival_time >= arrival_time_at_current_station - TimeSpan.FromMinutes(5)
                                    && train_1_departure_time <= arrival_time_at_current_station + halt_duration + TimeSpan.FromMinutes(5))
                                    || (halt_duration >= TimeSpan.FromMinutes(10) + train_1_halt_duration
                                    && halt_duration < TimeSpan.FromMinutes(20) + train_1_halt_duration
                                    && train_1_arrival_time >= arrival_time_at_current_station - TimeSpan.FromMinutes(2)
                                    && train_1_departure_time <= arrival_time_at_current_station + halt_duration + TimeSpan.FromMinutes(2)))
                                    {
                                        mw.WriteLine("\t\t\t\t\tOvertaken by " + train_1_number);
                                        train_overtaken_numbers.Add(train_1_number);
                                        no_of_overtakes = no_of_overtakes + 1;
                                    }
                                }
                            }

                            if (train_2_already_overtaken == false)
                            {
                                if (type_of_train_2 > train_type)
                                {
                                    if ((train_2_halt_duration >= TimeSpan.FromMinutes(20) + halt_duration
                                    && arrival_time_at_current_station >= train_2_arrival_time - TimeSpan.FromMinutes(5)
                                    && arrival_time_at_current_station + halt_duration <= train_2_departure_time + TimeSpan.FromMinutes(5))
                                    || (train_2_halt_duration >= TimeSpan.FromMinutes(10) + halt_duration
                                    && train_2_halt_duration < TimeSpan.FromMinutes(20) + halt_duration
                                    && arrival_time_at_current_station >= train_2_arrival_time - TimeSpan.FromMinutes(2)
                                    && arrival_time_at_current_station + halt_duration <= train_2_departure_time + TimeSpan.FromMinutes(2)))
                                    {
                                        mw.WriteLine("\t\t\t\t\tOvertakes " + train_2_number);
                                        train_overtaken_numbers.Add(train_2_number);
                                        no_of_overtakes = no_of_overtakes + 1;
                                    }
                                }
                                else
                                {
                                    if ((halt_duration >= TimeSpan.FromMinutes(20) + train_2_halt_duration
                                    && train_2_arrival_time >= arrival_time_at_current_station - TimeSpan.FromMinutes(5)
                                    && train_2_departure_time <= arrival_time_at_current_station + halt_duration + TimeSpan.FromMinutes(5))
                                    || (halt_duration >= TimeSpan.FromMinutes(10) + train_2_halt_duration
                                    && halt_duration < TimeSpan.FromMinutes(20) + train_2_halt_duration
                                    && train_2_arrival_time >= arrival_time_at_current_station - TimeSpan.FromMinutes(2)
                                    && train_2_departure_time <= arrival_time_at_current_station + halt_duration + TimeSpan.FromMinutes(2)))
                                    {
                                        mw.WriteLine("\t\t\t\t\tOvertaken by " + train_2_number);
                                        train_overtaken_numbers.Add(train_2_number);
                                        no_of_overtakes = no_of_overtakes + 1;
                                    }
                                }
                            }

                            if (no_of_overtakes == 0)
                            {
                                mw.WriteLine("\t\t\t\t\tNo Overtake Possible");
                                //  "break" causes the recursion to halt, hence no solution
                                break;
                            }
                        }
                        else
                        {
                            mw.WriteLine("\t\t\t\t\t3 or More Trains");
                            //  "break" causes the recursion to halt, hence no solution
                            break;
                        }

                        //  print statements for the purpose of debugging
                        Console.WriteLine("\n\t\t\t\t\t\t\t" + list_of_stations[previous_station_num] + "\t" + list_of_stations[current_station_num]
                                                        + "\t" + departure_time_at_previous_station + "\t" + arrival_time_at_current_station
                                                        + "\t" + no_of_platforms + "\t" + no_of_through_tracks);
                        if(list_of_stations[current_station_num].Length <= 3)
                            mw.Write("\n\t\t\t\t\t\t\t" + list_of_stations[current_station_num] + "\t\t" + arrival_time_at_current_station
                                                    + "\t\t" + departure_time_at_current_station + "\t\t" + no_of_platforms);
                        else
                            mw.Write("\n\t\t\t\t\t\t\t" + list_of_stations[current_station_num] + "\t" + arrival_time_at_current_station
                                                    + "\t\t" + departure_time_at_current_station + "\t\t" + no_of_platforms);

                        //  form a station pair of neighbouring stations along the route
                        row temp_pair = get_row_element(list_of_stations[previous_station_num], list_of_stations[current_station_num],
                                                            departure_time_at_previous_station, arrival_time_at_current_station,
                                                            arrival_day_num_at_current_station, no_of_platforms);

                        //  add this station pair to the "station_pairs" of our train
                        our_train.station_pairs.Add(temp_pair);

                        if (current_station_num != list_of_stations.Count() - 1)                            //  if current station is not the destination station 
                            return recurse_intermediate_stations(arrival_time_at_current_station + halt_duration,
                                                            days_of_departure_from_source, arrival_day_num_at_current_station,
                                                            current_station_num, list_of_stations, intermediate_halting_stations,
                                                            halt_duration_at_intermediate_halting_stations, avg_speed, our_train, train_overtaken_numbers,
                                                            sum_of_occupied_to_total_tracks, total_journey_length, journey_length_completed,
                                                            time_to_complete_journey, source_station_departure_time, train_type);
                        else                                                            //  if current station is the destination station
                        {
                            path_complete temp = new path_complete();                              //  we are making this composite object because  
                            temp.arrival_time_at_destination = arrival_time_at_current_station;    //  we needed to return arrival time at destination
                            temp.destination_station_code = list_of_stations[current_station_num]; //  as well as destination station code to check if
                            temp.our_train = our_train;                                            //  we reached in time to our destination or not
                            temp.sum_of_occupied_to_total_tracks = sum_of_occupied_to_total_tracks;
                            return temp;
                        }
                    }

                    else
                    {
                        mw.WriteLine("\n\t\t\t\t\tToo many trains at\t" + list_of_stations[current_station_num] + "\tat\t" + arrival_time_at_current_station);
                    }
                }
            }
            //  if a suitable window is not found return a dummy composite object
            path_complete temp_2 = new path_complete();                                             //  we are making this composite object because
            temp_2.arrival_time_at_destination = departure_time_at_previous_station;                //  we will check last station in train itinerary
            temp_2.destination_station_code = list_of_stations[current_station_num];                //  to confirm the correctness of the itinerary.
            temp_2.our_train = our_train;                                                           //  Here, the station returned is not the correct destination.
            return temp_2;
        }

        //  function returns train type of the train
        public int get_train_type(string train_number)
        {
            int train_type = 0;

            SqlConnection conn = new SqlConnection(conn_String);
            conn.Open();
            if (conn.State == System.Data.ConnectionState.Open)
            {
                string sql_query = "select TRAIN_TYPE from TRAIN where TRAIN_NO = '" + train_number + "'";
                SqlCommand cmd = new SqlCommand(sql_query, conn);
                train_type = (int)cmd.ExecuteScalar();
            }
            conn.Close();

            return train_type;
        }

        //  function returns summation of distance between intermediate neighbouring stations
        public double find_total_length_of_journey(List<string> list_of_stations)
        {
            double total_distance = 0;

            for(int i = 0; i < list_of_stations.Count - 1; i++)
            {
                //  add distance between current and next station to the total distance
                total_distance = total_distance + get_distance_between_neighbouring_stations(list_of_stations[i], list_of_stations[i + 1]);
            }

            return total_distance;
        }

        //  check if the given station is a halt or not
        //  a halt has in total 2 tracks or platforms
        public Boolean check_if_current_station_is_a_halt(String station_code)
        {
            Boolean is_current_station_a_halt = false;

            String [] station_code_of_halts = { "PWXP", "NDON", "VVG", "VKDH", "ASJP", "XSAN", "PATL", "GNHI", "SCY"};

            SqlConnection conn = new SqlConnection(conn_String);
            conn.Open();
            if (conn.State == System.Data.ConnectionState.Open)
            {
                if (station_code_of_halts.Contains(station_code))
                    is_current_station_a_halt = true;
            }
            conn.Close();

            return is_current_station_a_halt;
        }

        //  function returns train number of train that arrives first at given station
        public String get_train_arriving_first(string train_1_number, string train_2_number, string station_code)
        {
            DateTime train_1_arrival_time = get_time(get_arrival_time_of_train_at_station(train_1_number, station_code));
            DateTime train_2_arrival_time = get_time(get_arrival_time_of_train_at_station(train_2_number, station_code));

            if (train_1_arrival_time < train_2_arrival_time)
                return train_1_number;
            else
                return train_2_number;
        }

        //  function returns train number of train that overtakes
        public String find_overtaking_train(string train_1_number, string train_2_number)
        {
            Int32 temp = 0;

            SqlConnection conn = new SqlConnection(conn_String);
            conn.Open();
            if (conn.State == System.Data.ConnectionState.Open)
            {
                string sql_query = "select count(*) from OVERTAKE where TRAIN_1_CODE = '"
                                        + train_1_number + "' AND TRAIN_2_CODE = '" +
                                        train_2_number + "'";
                SqlCommand cmd = new SqlCommand(sql_query, conn);
                temp = (Int32)cmd.ExecuteScalar();
            }
            conn.Close();

            if (temp > 0)
                return train_1_number;
            else
                return train_2_number;
        }

        //  function returns true if an overtake is happening considering given parameters
        public bool is_overtake_happening(String station_code, List<String> overlapping_trains)
        {
            int number_of_overtakes = 0;

            SqlConnection conn = new SqlConnection(conn_String);
            conn.Open();
            if (conn.State == System.Data.ConnectionState.Open)
            {
                foreach (var train_number in overlapping_trains)
                {
                    string sql_query = "select count(*) from OVERTAKE where STATION_CODE = '" + station_code
                                            + "' AND (TRAIN_1_CODE = '" + train_number
                                            + "' OR TRAIN_2_CODE = '" + train_number
                                            + "' )";
                    SqlCommand cmd = new SqlCommand(sql_query, conn);
                    Int32 temp = (Int32)cmd.ExecuteScalar();

                    number_of_overtakes = number_of_overtakes + temp;
                }

            }
            conn.Close();

            if (number_of_overtakes == 0)
                return false;
            else
                return true;
        }

        //  function returns speed of the train with the given train number 
        public double get_speed_of_train(string train_number)
        {
            Double speed_of_train = 0;

            SqlConnection conn = new SqlConnection(conn_String);
            conn.Open();
            if (conn.State == System.Data.ConnectionState.Open)
            {
                string sql_query = "select AVERAGE_SPEED from TRAIN where TRAIN_NO = '" + train_number + "'";
                SqlCommand cmd = new SqlCommand(sql_query, conn);
                speed_of_train = (Double)cmd.ExecuteScalar();
            }
            conn.Close();

            return speed_of_train;
        }

        //  function returns arrival time of the given train at the given station
        public String get_arrival_time_of_train_at_station(String train_number, String station_code)
        {
            String arrival_time = "";
            Console.WriteLine(train_number + "\t" + station_code);

            SqlConnection conn = new SqlConnection(conn_String);
            conn.Open();
            if (conn.State == System.Data.ConnectionState.Open)
            {
                string sql_query = "select count(*) from USES where STATION_CODE = '" + station_code
                                            + "' AND TRAIN_NO = '" + train_number + "'";
                SqlCommand cmd = new SqlCommand(sql_query, conn);
                Int32 temp = (Int32)cmd.ExecuteScalar();

                if(temp == 1)
                {
                    string sql_query_1 = "select ARRIVAL_TIME from USES where STATION_CODE = '" + station_code
                                            + "' AND TRAIN_NO = '" + train_number + "'";
                    SqlCommand cmd_1 = new SqlCommand(sql_query_1, conn);
                    arrival_time = (String)cmd_1.ExecuteScalar();
                }
                
            }
            conn.Close();

            Console.WriteLine("\tArrival time\t" + arrival_time + ".");
            return arrival_time;
        }

        //  function returns departure time of the given train at the given station
        public String get_departure_time_of_train_at_station(String train_number, String station_code)
        {
            String departure_time = "";

            SqlConnection conn = new SqlConnection(conn_String);
            conn.Open();
            if (conn.State == System.Data.ConnectionState.Open)
            {
                string sql_query = "select count(*) from USES where STATION_CODE = '" + station_code
                                            + "' AND TRAIN_NO = '" + train_number + "'";
                SqlCommand cmd = new SqlCommand(sql_query, conn);
                Int32 temp = (Int32)cmd.ExecuteScalar();

                if(temp == 1)
                {
                    string sql_query_1 = "select DEPARTURE_TIME from USES where STATION_CODE = '" + station_code
                                            + "' AND TRAIN_NO = '" + train_number + "'";
                    SqlCommand cmd_1 = new SqlCommand(sql_query_1, conn);
                    departure_time = (String)cmd_1.ExecuteScalar();
                }
                
            }
            conn.Close();

            return departure_time;
        }

        //  function returns the number of overlapping trains of
        //  the day in the week with the maximum number of overlappings
        public List<String> get_train_no_on_day_of_maximum_overlaps(List<string> overlapping_trains, String station_code, Char[] days_of_arrival_of_our_train)
        {
            List<String> train_number_list = new List<String>();

            //  //  integer array to store number of overlappings day wise
            int[] overlaps = { 0, 0, 0, 0, 0, 0, 0 };

            foreach (var train in overlapping_trains)
            {
                //  find number of overlaps on each day
                char[] days_of_arrival = get_days_of_arrival(train, station_code);
                for (int i = 0; i < 7; i++)
                {
                    if (days_of_arrival[i] != 'N')
                        overlaps[i]++;
                }
            }

            int maximum_no_of_overlaps = 0;
            int day_number_with_maximum_overlap = 0;

            //  find day number on which maximum overlap happens
            for (int i = 0; i < 7; i++)
            {
                if (days_of_arrival_of_our_train[i] != 'N' && maximum_no_of_overlaps < overlaps[i])
                {
                    maximum_no_of_overlaps = overlaps[i];
                    day_number_with_maximum_overlap = i;
                }
            }

            //  find trains that run on the day of maximum overlap
            foreach (var train_number in overlapping_trains)
            {
                char[] days_of_arrival = get_days_of_arrival(train_number, station_code);
                if (days_of_arrival[day_number_with_maximum_overlap] != 'N')
                    train_number_list.Add(train_number);
            }

            return train_number_list;
        }

        //  function returns list of train codes of trains that move in same direction as our train
        public List<string> get_list_of_overlapping_trains_that_move_in_same_direction(List<string> overlapping_trains,
                                                                            List<String> list_of_stations, int currrent_station_num)
        {
            String previous_station_code = list_of_stations[currrent_station_num - 1];
            String current_station_code = list_of_stations[currrent_station_num];
            String next_station_code = "DWJN";

            //  check if end of station list has not reached
            if((currrent_station_num + 1) < list_of_stations.Count)
                next_station_code = list_of_stations[currrent_station_num + 1];

            List<String> overlapping_trains_in_same_direction = new List<String>();

            SqlConnection conn = new SqlConnection(conn_String);
            conn.Open();
            if (conn.State == System.Data.ConnectionState.Open)
            {
                foreach (var train in overlapping_trains)
                {
                    //  find trains that visit both - previous station and current station
                    string sql_query_1 = "select count(*) from USES where STATION_CODE = '" + previous_station_code
                                      + "'AND TRAIN_NO = '" + train + "'";
                    SqlCommand cmd_1 = new SqlCommand(sql_query_1, conn);
                    Int32 count = (Int32)cmd_1.ExecuteScalar();

                    Console.Write("\n\n\t\t\t" + train);

                    //  if the train visits both the stations - current and previous
                    if (count == 1)
                    {
                        Console.Write("\t\t\t" + train);

                        //  find distance travelled by train at the current station
                        string sql_query_2 = "select DISTANCE_TRAVELLED from USES where STATION_CODE = '" + current_station_code
                                          + "'AND TRAIN_NO = '" + train + "'";
                        SqlCommand cmd_2 = new SqlCommand(sql_query_2, conn);
                        double current_station_distance = (double)cmd_2.ExecuteScalar();

                        //  find distance travelled by train at previous station
                        string sql_query_3 = "select DISTANCE_TRAVELLED from USES where STATION_CODE = '" + previous_station_code
                                          + "'AND TRAIN_NO = '" + train + "'";
                        SqlCommand cmd_3 = new SqlCommand(sql_query_3, conn);
                        double previous_station_distance = (double)cmd_3.ExecuteScalar();

                        //  this shows station_2 comes after station_1 in the journey for our "train"
                        if (current_station_distance > previous_station_distance)
                        {
                            overlapping_trains_in_same_direction.Add(train);
                            Console.WriteLine("\t\t\t" + train);
                        }
                    }
                }

                foreach (var train in overlapping_trains)
                {
                    //  if train has already been added via above loop, jump to the next train
                    if (overlapping_trains_in_same_direction.Contains(train))
                        continue;

                    //  find trains that visit both - current station and next station
                    string sql_query_1 = "select count(*) from USES where STATION_CODE = '" + next_station_code
                                      + "'AND TRAIN_NO = '" + train + "'";
                    SqlCommand cmd_1 = new SqlCommand(sql_query_1, conn);
                    Int32 count = (Int32)cmd_1.ExecuteScalar();

                    Console.Write("\n\n\t\t\t" + train);

                    //  if the train visits both the stations - current and next
                    if (count == 1)
                    {
                        Console.Write("\t\t\t" + train);

                        //  find distance travelled by train at the current station
                        string sql_query_2 = "select DISTANCE_TRAVELLED from USES where STATION_CODE = '" + current_station_code
                                          + "'AND TRAIN_NO = '" + train + "'";
                        SqlCommand cmd_2 = new SqlCommand(sql_query_2, conn);
                        double current_station_distance = (double)cmd_2.ExecuteScalar();

                        //  find distance travelled by train at next station
                        string sql_query_3 = "select DISTANCE_TRAVELLED from USES where STATION_CODE = '" + next_station_code
                                          + "'AND TRAIN_NO = '" + train + "'";
                        SqlCommand cmd_3 = new SqlCommand(sql_query_3, conn);
                        double next_station_distance = (double)cmd_3.ExecuteScalar();

                        //  this shows station_2 comes after station_1 in the journey for our "train"
                        if (current_station_distance < next_station_distance)
                        {
                            overlapping_trains_in_same_direction.Add(train);
                            Console.WriteLine("\t\t\t" + train);
                        }
                    }
                }
            }
            conn.Close();

            return overlapping_trains_in_same_direction;
        }

        //  function returns the number of overlapping trains of
        //  the day with the maximum number of overlappings
        public int get_maximum_no_of_overlaps(List<string> overlapping_trains, string station_code)
        {
            int maximum_no_of_overlaps = 0;
            int[] overlaps = { 0, 0, 0, 0, 0, 0, 0 };                                               //  integer array to store number of overlappings day wise
            foreach (var train in overlapping_trains)
            {
                char[] days_of_arrival = get_days_of_arrival(train, station_code);
                for (int i = 0; i < 7; i++)
                {
                    if (days_of_arrival[i] != 'N')
                        overlaps[i]++;
                }
            }
            foreach (var count in overlaps)
            {
                if (maximum_no_of_overlaps < count)
                    maximum_no_of_overlaps = count;
            }
            return maximum_no_of_overlaps;
        }

        //  function returns a row of the inter-station movement,
        //  only between the neighbouring stations along the route
        public row get_row_element(string station_1_code, string station_2_code, DateTime departure_time_at_station_1,
                                        DateTime arrival_time_at_station_2, int arrival_day_num_at_station_2, int num_of_platforms_at_station_2)
        {
            row neighbouring_stations_pair = new row();

            neighbouring_stations_pair.station_1_code = station_1_code;
            neighbouring_stations_pair.station_2_code = station_2_code;
            neighbouring_stations_pair.station_1_departure_time = departure_time_at_station_1;
            neighbouring_stations_pair.station_2_arrival_time = arrival_time_at_station_2;
            neighbouring_stations_pair.arrival_day_num_at_station_2 = arrival_day_num_at_station_2;
            neighbouring_stations_pair.num_platforms_at_station_2 = num_of_platforms_at_station_2;

            return neighbouring_stations_pair;
        }

        //  function returns a list of train numbers (after filtering from 
        //  "train_number_of_overlapping_trains") which halt at the given station
        public List<string> find_overlapping_trains_that_halt(List<string> train_number_of_overlapping_trains, string station_code)
        {
            List<String> train_number_of_overlapping_trains_that_halt = new List<String>();

            SqlConnection conn = new SqlConnection(conn_String);
            conn.Open();
            if (conn.State == System.Data.ConnectionState.Open)
            {
                foreach (var train_number_of_overlapping_train in train_number_of_overlapping_trains)
                {
                    string sql_query_1 = "select count(*) from USES where STATION_CODE = '" + station_code
                                            + "' AND TRAIN_NO = '" + train_number_of_overlapping_train
                                            + "' AND NOT (ARRIVAL_TIME = DEPARTURE_TIME)";
                    SqlCommand cmd_1 = new SqlCommand(sql_query_1, conn);
                    Int32 num = (Int32)cmd_1.ExecuteScalar();

                    if (num > 0)
                        train_number_of_overlapping_trains_that_halt.Add(train_number_of_overlapping_train);
                }

                foreach (var train_number_of_overlapping_train_that_halts in train_number_of_overlapping_trains_that_halt)
                {
                    Console.WriteLine("\t\t\t" + train_number_of_overlapping_train_that_halts);
                }
            }
            conn.Close();

            return train_number_of_overlapping_trains_that_halt;
        }

        //  function checks if "list_of_stations[current_station_num]" belongs 
        //  to intermediate_halting_stations
        //  if it does not belong returns -1
        //  else returns index of "intermediate_halting_stations"
        public int is_halting_station(int current_station_num, List<string> list_of_stations, string[] intermediate_halting_stations)
        {
            int intermediate_halting_station_num = -1;

            for (int i = 0; i < intermediate_halting_stations.Count(); i++)
            {
                if (String.Compare(intermediate_halting_stations[i], list_of_stations[current_station_num], true) == 0)
                {
                    intermediate_halting_station_num = i;
                    break;
                }
            }

            return intermediate_halting_station_num;
        }

        //  function returns arrival day number at current station depending upon
        //  departure time at previous station, arrival time at current station
        //  and departure day number at previous station
        public int get_arrival_day_num_at_current_station(DateTime departure_time_at_previous_station,
                                            DateTime tentative_arrival_time_at_current_station, int departure_day_num_at_previous_station)
        {
            String tentative_arrival_time_at_current_station_string = tentative_arrival_time_at_current_station.ToString("HH:mm", CultureInfo.CurrentCulture);
            String departure_time_at_previous_station_string = departure_time_at_previous_station.ToString("HH:mm", CultureInfo.CurrentCulture);

            int arrival_day_num_at_current_station = departure_day_num_at_previous_station;
            if (String.Compare(tentative_arrival_time_at_current_station_string, departure_time_at_previous_station_string, true) < 0)
                arrival_day_num_at_current_station = arrival_day_num_at_current_station + 1;

            return arrival_day_num_at_current_station;
        }

        //  function returns days of arrival as a character array depending upon 
        //  the offset from day of departure which is arrival day number
        public char[] find_days_of_arrival(char[] days_of_departure_from_source, int arrival_day_num_at_current_station)
        {
            char[] days_of_arrival = { 'N', 'N', 'N', 'N', 'N', 'N', 'N' };
            char[] week_days = { 'S', 'M', 'T', 'W', 'T', 'F', 'S' };

            for (int i = 0; i < 7; i++)
            {
                if (days_of_departure_from_source[i] != 'N')
                {
                    days_of_arrival[(i + arrival_day_num_at_current_station - 1) % 7] = week_days[(i + arrival_day_num_at_current_station - 1) % 7];
                }
            }

            return days_of_arrival;
        }

        //  function returns distance between the two given neghbouring stations
        public double get_distance_between_neighbouring_stations(string station_1_code, string station_2_code)
        {
            //  first_station_code should appear before second_station_code in dictionary

            //  assume station_1_code to appear before station_2_code in dictionary
            string first_station_code = station_1_code;
            string second_station_code = station_2_code;

            //  if assumption is wrong, exchange the assignments
            var lexical_order = String.Compare(station_1_code, station_2_code, true);
            if (lexical_order > 0)
            {
                first_station_code = station_2_code;
                second_station_code = station_1_code;
            }

            double distance = 0;

            SqlConnection conn = new SqlConnection(conn_String);
            conn.Open();
            if (conn.State == System.Data.ConnectionState.Open)
            {
                String sql_query_1 = "select DISTANCE from NEIGHBOUR where STATION_1_CODE = '" + first_station_code
                                           + "' and STATION_2_CODE = '" + second_station_code + "'";
                SqlCommand cmd_1 = new SqlCommand(sql_query_1, conn);
                distance = (double)cmd_1.ExecuteScalar();

                Console.WriteLine("\n\t\tDistance : " + distance);
            }
            conn.Close();

            return distance;
        }

        //  function returns a list of train numbers that have overlapping 
        //  departure times at given station on given week days and time
        public List<String> find_overlapping_departures(DateTime departure_time, char[] days_of_origin, String station_code)
        {
            //  our train leaves at the middle of begin_time and finish_time
            //  Consider a window of 10 minutes because in order to depart you need the track free for 5 minutes before you leave
            //  and for 5 minutes after you leave. So, no other train should be departing between begin_time & finish_time.
            String begin_time = departure_time.ToString("HH:mm", CultureInfo.CurrentCulture);
            String finish_time = (departure_time.Add(TimeSpan.FromMinutes(10))).ToString("HH:mm", CultureInfo.CurrentCulture);

            List<String> details_of_train_with_overlapping_departure = new List<string>();              //  details have been stored for the purpose of debugging
            List<String> train_number_of_train_with_overlapping_departure = new List<String>();          //  train numbers of overlapping trains is what we need

            SqlConnection conn = new SqlConnection(conn_String);
            conn.Open();
            if (conn.State == System.Data.ConnectionState.Open)
            {
                string sql_query_1 = "select * from USES where STATION_CODE = '" + station_code
                                      + "'AND (DEPARTURE_TIME >= '" + begin_time
                                      + "'AND DEPARTURE_TIME <= '" + finish_time + "') ORDER BY ARRIVAL_TIME";
                SqlCommand cmd_1 = new SqlCommand(sql_query_1, conn);
                SqlDataReader myreader = cmd_1.ExecuteReader();

                Console.WriteLine("\n\t" + begin_time + "\t" + finish_time);

                //  run a loop to access each of the row myreader has
                while (myreader.Read())
                {
                    //  fetch week days on which the train arrives at this station
                    //  myreader[0].ToString() is train number
                    //  myreader[1].ToString() is station code
                    char[] days_of_arrival = get_days_of_arrival(myreader[0].ToString(), myreader[1].ToString());

                    //  check if this train arrives on same week day as our train
                    //  if it does, then add the details and train number
                    if (have_any_common_day(days_of_arrival, days_of_origin))
                    {
                        train_number_of_train_with_overlapping_departure.Add(myreader[0].ToString());
                        details_of_train_with_overlapping_departure.Add(myreader[0].ToString() + "\t" + myreader[1].ToString() + "\t" + myreader[2].ToString()
                                            + "\t" + myreader[3].ToString() + "\t" + myreader[4].ToString()
                                            + "\t" + myreader[5].ToString() + "\t" + myreader[6].ToString());
                    }
                }

                foreach (var train in details_of_train_with_overlapping_departure)
                {
                    Console.WriteLine("\t\t\t" + train);        //  print the train details
                }
            }
            conn.Close();

            return train_number_of_train_with_overlapping_departure;
        }

        //  function returns list of train codes that overlap the given time range at given station on concerned week days
        public List<String> get_list_of_trains_overlapping_on_concerned_days_and_time_range(char[] days_of_arrival_of_our_train, String begin_time,
                                                                                String finish_time, String station_code)
        {
            List<String> train_details_of_overlapping_trains = new List<String>();
            List<String> train_numbers_of_overlapping_trains = new List<string>();

            SqlConnection conn = new SqlConnection(conn_String);
            conn.Open();
            if (conn.State == System.Data.ConnectionState.Open)
            {
                //  find train details of overlapping trains

                string sql_query_1 = "select * from USES where STATION_CODE = '" + station_code
                                      + "'AND ((ARRIVAL_TIME >= '" + begin_time
                                      + "'AND ARRIVAL_TIME <= '" + finish_time
                                      + "') OR (DEPARTURE_TIME >= '" + begin_time
                                      + "'AND DEPARTURE_TIME <= '" + finish_time
                                      + "') OR (ARRIVAL_TIME < '" + begin_time
                                      + "'AND DEPARTURE_TIME > '" + finish_time + "')) ORDER BY ARRIVAL_TIME";

                int lex = String.Compare(begin_time, finish_time);

                //  such trains would have begin time before midnight
                //  and finish time after midnight
                if (lex > 0)
                {
                    sql_query_1 = "select * from USES where STATION_CODE = '" + station_code
                                      + "'AND ((ARRIVAL_TIME >= '" + begin_time
                                      + "'AND ARRIVAL_TIME <= '" + "23:59"
                                      + "') OR (ARRIVAL_TIME >= '" + "00:00"
                                      + "'AND ARRIVAL_TIME <= '" + finish_time
                                      + "') OR (DEPARTURE_TIME >= '" + begin_time
                                      + "'AND DEPARTURE_TIME <= '" + "23:59"
                                      + "') OR (DEPARTURE_TIME >= '" + "00:00"
                                      + "'AND DEPARTURE_TIME <= '" + finish_time
                                      + "') OR (ARRIVAL_TIME < '" + begin_time
                                      + "'AND DEPARTURE_TIME > '" + finish_time
                                      + "'AND ARRIVAL_TIME > DEPARTURE_TIME)) ORDER BY ARRIVAL_TIME";
                }

                SqlCommand cmd_1 = new SqlCommand(sql_query_1, conn);
                SqlDataReader myreader = cmd_1.ExecuteReader();

                while (myreader.Read())
                {
                    char[] days_of_arrival = get_days_of_arrival(myreader[0].ToString(), myreader[1].ToString());
                    if (have_any_common_day(days_of_arrival, days_of_arrival_of_our_train))
                    {
                        train_numbers_of_overlapping_trains.Add(myreader[0].ToString());
                        train_details_of_overlapping_trains.Add(myreader[0].ToString() + "\t" + myreader[1].ToString() + "\t" + myreader[2].ToString()
                                            + "\t" + myreader[3].ToString() + "\t" + myreader[4].ToString()
                                            + "\t" + myreader[5].ToString() + "\t" + myreader[6].ToString());
                    }
                }
                //mw.WriteLine("\t" + train_details_of_overlapping_trains.Count);
                foreach (var train in train_details_of_overlapping_trains)
                {
                    Console.WriteLine("\t" + train);
                    //mw.WriteLine("\t" + train);
                }
            }
            conn.Close();

            return train_numbers_of_overlapping_trains;
        }

        //  function returns the shortest path as a List of String of Station Codes between given stations (end stations inclusive)
        public List<string> get_shortest_path(string source_station_code, string destination_station_code, string[] intermediate_halting_stations)
        {
            List<String> stations_on_shortest_path = new List<String>();

            SqlConnection conn = new SqlConnection(conn_String);
            conn.Open();
            if (conn.State == System.Data.ConnectionState.Open)
            {
                //  find the trains (train numbers) with given source and destination station
                String sql_query_1 = "select TRAIN_NO from TRAIN where SOURCE_STATION_CODE = '" + source_station_code
                                            + "' AND DESTINATION_STATION_CODE = '" + destination_station_code
                                            + "' ORDER BY LENGTH_OF_JOURNEY ASC";
                SqlCommand cmd_1 = new SqlCommand(sql_query_1, conn);
                SqlDataReader myreader_1 = cmd_1.ExecuteReader();

                //  get trains (train numbers) from reader
                List<String> trains_with_same_source_destination_pair = new List<String>();
                while (myreader_1.Read())
                {
                    trains_with_same_source_destination_pair.Add(myreader_1[0].ToString());
                }

                //  assume i to be the index for intermediate halting stations
                int i = 0;
                foreach (var train_num in trains_with_same_source_destination_pair)
                {
                    i = 0;
                    stations_on_shortest_path.Clear();

                    String sql_query_3 = "select STATION_CODE from USES where TRAIN_NO = '" + train_num + "' ORDER BY DISTANCE_TRAVELLED ASC";
                    SqlCommand cmd_3 = new SqlCommand(sql_query_3, conn);
                    SqlDataReader myreader_3 = cmd_3.ExecuteReader();

                    while (myreader_3.Read())
                    {
                        //  add stations to the list
                        stations_on_shortest_path.Add(myreader_3[0].ToString());

                        //  enter the condition if some intermediate halting station is unvisited
                        if (i < intermediate_halting_stations.Length)
                        {
                            if (intermediate_halting_stations[i].Equals(myreader_3[0].ToString()))
                                i++;
                        }
                    }

                    if (i == intermediate_halting_stations.Length)
                        break;
                }
            }
            conn.Close();

            return stations_on_shortest_path;
        }

        //  function returns true if the two week arrays have any common day otherwise false 
        public bool have_any_common_day(char[] days_of_arrival, char[] days_of_origin)
        {
            bool have_any_common_day = false;
            for (int i = 0; i < 7; i++)
            {
                if (days_of_origin[i] != 'N' && days_of_arrival[i] != 'N')
                {
                    have_any_common_day = true;
                    break;
                }
            }
            return have_any_common_day;
        }

        //  function returns days of arrival of a train at a station as an array of characters
        public char[] get_days_of_arrival(string train_no, string station_code)
        {
            // this character array will be returned after appropriate modifications
            char[] days_of_arrival = { 'N', 'N', 'N', 'N', 'N', 'N', 'N' };

            SqlConnection conn = new SqlConnection(conn_String);
            conn.Open();
            if (conn.State == System.Data.ConnectionState.Open)
            {
                // fetch days of departure from source station
                String sql_query_1 = "select SUNDAY, MONDAY, TUESDAY, WEDNESDAY, THURDAY, FRIDAY, SATURDAY, TRAIN_NO from TRAIN where TRAIN_NO = '"
                                    + train_no + "'";
                SqlCommand cmd_1 = new SqlCommand(sql_query_1, conn);
                SqlDataReader myreader = cmd_1.ExecuteReader();

                //  feed days of departure from source station to days_of_departure_from_source
                List<Char> days_of_departure_from_source = new List<char>();
                while (myreader.Read())
                {
                    days_of_departure_from_source.Add(myreader[0].ToString()[0]);
                    days_of_departure_from_source.Add(myreader[1].ToString()[0]);
                    days_of_departure_from_source.Add(myreader[2].ToString()[0]);
                    days_of_departure_from_source.Add(myreader[3].ToString()[0]);
                    days_of_departure_from_source.Add(myreader[4].ToString()[0]);
                    days_of_departure_from_source.Add(myreader[5].ToString()[0]);
                    days_of_departure_from_source.Add(myreader[6].ToString()[0]);
                }

                // fetch the day number of arrival of the train at the required station
                String sql_query_2 = "select ARRIVAL_DAY from USES where TRAIN_NO = '" + train_no + "' and STATION_CODE = '" + station_code + "'";
                SqlCommand cmd_2 = new SqlCommand(sql_query_2, conn);
                Int32 arrival_day_num = (Int32)cmd_2.ExecuteScalar();

                Char[] week_days = { 'S', 'M', 'T', 'W', 'T', 'F', 'S' };
                for (int i = 0; i < 7; i++)
                {
                    if (days_of_departure_from_source[i] != 'N')
                    {
                        days_of_arrival[(i + arrival_day_num - 1) % 7] = week_days[(i + arrival_day_num - 1) % 7];
                    }
                }
            }
            conn.Close();

            return days_of_arrival;
        }

        //  function returns no. of platforms at the given station
        public int get_no_of_platforms(String station_code)
        {
            int no_of_platforms = 0;

            SqlConnection conn = new SqlConnection(conn_String);
            conn.Open();
            if (conn.State == System.Data.ConnectionState.Open)
            {
                String sql_query = "select NO_OF_PLATFORMS from STATION where STATION_CODE = '" + station_code + "'";
                SqlCommand cmd = new SqlCommand(sql_query, conn);
                no_of_platforms = (Int32)cmd.ExecuteScalar();
            }
            conn.Close();

            return no_of_platforms;
        }

        //  function returns time in DateTime type using argument time in string form "hh:mm"
        public DateTime get_time(String time)
        {
            return DateTime.ParseExact(time, "HH:mm", CultureInfo.CurrentCulture);
        }
    }
}