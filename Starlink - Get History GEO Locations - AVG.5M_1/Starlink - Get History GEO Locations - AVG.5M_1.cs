namespace DSStarlinkGeoHistoryLocations
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Skyline.DataMiner.Analytics.GenericInterface;
    using Skyline.DataMiner.Net;
    using Skyline.DataMiner.Net.Messages;
    using Skyline.DataMiner.Net.Trending;

    /// <summary>
    /// Represents a data source.
    /// See: https://aka.dataminer.services/gqi-external-data-source for a complete example.
    /// </summary>
    [GQIMetaData(Name = "Starlink - Get History GEO Locations - AVG.5M")]
    public sealed class DSStarlinkGeoHistoryLocations : IGQIDataSource
        , IGQIOnInit
        , IGQIInputArguments
    {
        private const int ColumnParameterIdStarlinkEnterpriseLatitude = 1213;
        private const int ColumnParameterIdStarlinkEnterpriseLongitude = 1214;
        private const double ExceptionValueDoubleNA = -1.0;
        private const int MaximumOfRecordsToSample = 1000; // Ensure max number of records in the response

        private readonly GQIStringArgument starlinkEnterpriseElementIdArg = new GQIStringArgument("Starlink Enterprise Element ID")
        {
            IsRequired = true,
        };

        private readonly GQIStringArgument userTerminalDeviceIdArg = new GQIStringArgument("User Terminal Device ID")
        {
            IsRequired = true,
        };

        private readonly GQIDateTimeArgument timeSpanStartArgX = new GQIDateTimeArgument("History Time Range Start")
        {
            IsRequired = true,
        };

        private readonly GQIDateTimeArgument timeSpanEndArgX = new GQIDateTimeArgument("History Time Range End")
        {
            IsRequired = true,
        };

        private readonly List<GQIRow> listGqiRows = new List<GQIRow> { };

        private GQIDMS _dms;

        public static DateTime RoundToNearest5Min(DateTime time)
        {
            int minutes = (time.Minute / 5) * 5;  // Always round down
            return new DateTime(time.Year, time.Month, time.Day, time.Hour, minutes, 0);
        }

        public OnInitOutputArgs OnInit(OnInitInputArgs args)
        {
            _dms = args.DMS;
            return new OnInitOutputArgs();
        }

        public GQIArgument[] GetInputArguments()
        {
            return new GQIArgument[]
            {
                starlinkEnterpriseElementIdArg,
                userTerminalDeviceIdArg,
                timeSpanStartArgX,
                timeSpanEndArgX,
            };
        }

        public OnArgumentsProcessedOutputArgs OnArgumentsProcessed(OnArgumentsProcessedInputArgs args)
        {
            var starttime = args.GetArgumentValue(timeSpanStartArgX);
            var endtime = args.GetArgumentValue(timeSpanEndArgX);

            listGqiRows.Clear();
            var starlinkEnterpriseElement = args.GetArgumentValue(starlinkEnterpriseElementIdArg);
            var userTerminalIdPk = args.GetArgumentValue(userTerminalDeviceIdArg);

            var dmsElementID = starlinkEnterpriseElement.Split('/');
            var dmaID = Convert.ToInt32(dmsElementID[0]);
            var elementID = Convert.ToInt32(dmsElementID[1]);

            var parameterPairs = new ParameterIndexPair[] { new ParameterIndexPair(ColumnParameterIdStarlinkEnterpriseLatitude, userTerminalIdPk), new ParameterIndexPair(ColumnParameterIdStarlinkEnterpriseLongitude, userTerminalIdPk) };
            GetTrendDataMessage trendMessage = new GetTrendDataMessage
            {
                DataMinerID = dmaID,
                ElementID = elementID,
                Parameters = parameterPairs,
                StartTime = starttime,
                EndTime = endtime,
                TrendingType = TrendingType.Average,
                AverageTrendIntervalType = AverageTrendIntervalType.FiveMin,
                Raw = true,
                ReturnAsObjects = true,
            };

            var trendDataResponseMessage = _dms.SendMessage(trendMessage) as GetTrendDataResponseMessage;
            if (trendDataResponseMessage == null || trendDataResponseMessage.Records == null)
            {
                return new OnArgumentsProcessedOutputArgs();
            }

            ProcessTrendResponseResult(trendDataResponseMessage);

            return new OnArgumentsProcessedOutputArgs();
        }

        public GQIColumn[] GetColumns()
        {
            return new GQIColumn[]
          {
                new GQIStringColumn("ID"),
                new GQIDoubleColumn("Sorting"),
                new GQIDoubleColumn("Latitude"),
                new GQIDoubleColumn("Longitude"),
                new GQIDateTimeColumn("TimeStamp"),
          };
        }

        public GQIPage GetNextPage(GetNextPageInputArgs args)
        {
            return new GQIPage(listGqiRows.ToArray())
            {
                HasNextPage = false,
            };
        }

        private void ProcessTrendResponseResult(GetTrendDataResponseMessage trendDataResponseMessage)
        {
            // Dictionary to store Latitude and Longitude values by rounded timestamps
            var latitudeDict = new Dictionary<DateTime, double>();
            var longitudeDict = new Dictionary<DateTime, double>();
            int trendRecordTypeOfInterest = 5; // Five Minute Trend records

            // Step 1: Collect Latitude Data
            string latitudeMatch = Convert.ToString(ColumnParameterIdStarlinkEnterpriseLatitude);
            foreach (var record in trendDataResponseMessage.Records)
            {
                if (!record.Value.Any())
                {
                    continue;
                }

                if (record.Key.StartsWith(latitudeMatch)) // latitude
                {
                    foreach (var entry in record.Value)
                    {
                        if (entry is AverageTrendRecord avgTimeData && avgTimeData.Status == trendRecordTypeOfInterest)
                        {
                            DateTime roundedTime = RoundToNearest5Min(avgTimeData.Time);
                            latitudeDict[roundedTime] = avgTimeData.AverageValue;
                        }
                    }
                }
            }

            // Step 2: Collect Longitude Data
            string longitudeMatch = Convert.ToString(ColumnParameterIdStarlinkEnterpriseLongitude);
            foreach (var record in trendDataResponseMessage.Records)
            {
                if (!record.Value.Any())
                {
                    continue;
                }

                if (record.Key.StartsWith(longitudeMatch)) // Longitude
                {
                    int samplingRate = (int)Math.Ceiling((double)record.Value.Count / MaximumOfRecordsToSample);  // Ensure max number of records

                    for (int i = 0; i < record.Value.Count; i += samplingRate)
                    {
                        if (record.Value[i] is AverageTrendRecord avgTimeData && avgTimeData.Status == trendRecordTypeOfInterest)
                        {
                            DateTime roundedTime = RoundToNearest5Min(avgTimeData.Time);
                            longitudeDict[roundedTime] = avgTimeData.AverageValue;
                        }
                    }
                }
            }

            // Step 3: Match Latitude & Longitude by Timestamps
            var index = 0;
            foreach (var time in longitudeDict.Keys.Where(latitudeDict.ContainsKey)) // Only matching timestamps
            {
                double latitude = latitudeDict[time];
                double longitude = longitudeDict[time];

                if (latitude != ExceptionValueDoubleNA && longitude != ExceptionValueDoubleNA) // Ensure valid data
                {
                    AddResultRow(index++, time, latitude, longitude);
                }
            }

            ModifyLastResultItemIdentifier();
        }

        private void ModifyLastResultItemIdentifier()
        {
            if (listGqiRows.Any())
            {
                // Override the last result ID with value 1000, This makes it easier to know the last data sample.
                listGqiRows.Last().Cells[0].Value = "1000";
            }
        }

        private void AddResultRow(int index, DateTime timeStamp, double latitude, double longitude)
        {
            var gqiRow = new[]
            {
                new GQICell
                {
                    Value = Convert.ToString(index),
                },
                new GQICell
                {
                    Value = Convert.ToDouble(index),
                },
                new GQICell
                {
                    Value = latitude,
                },
                new GQICell
                {
                    Value = longitude,
                },
                new GQICell
                {
                    Value = timeStamp.ToUniversalTime(),
                },
            };

            listGqiRows.Add(new GQIRow(gqiRow));
        }
    }
}
