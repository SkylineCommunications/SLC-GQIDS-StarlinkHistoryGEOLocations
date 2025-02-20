namespace DSStarlinkGeoHistoryLocations
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Security.Cryptography;
    using Skyline.DataMiner.Analytics.GenericInterface;
    using Skyline.DataMiner.Net;
    using Skyline.DataMiner.Net.Messages;
    using Skyline.DataMiner.Net.Trending;
    using SLDataGateway.API.Collections.Linq;

    /// <summary>
    /// Represents a data source.
    /// See: https://aka.dataminer.services/gqi-external-data-source for a complete example.
    /// </summary>
    [GQIMetaData(Name = "Starlink - Get History GEO Locations - AVG.5M")]
    public sealed class DSStarlinkGeoHistoryLocations : IGQIDataSource
        , IGQIOnInit
        , IGQIInputArguments
    {
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

        private GQIDMS _dms;
        private string starlinkEnterpriseElement = String.Empty;
        private List<GQIRow> listGqiRows = new List<GQIRow> { };

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
            starlinkEnterpriseElement = args.GetArgumentValue(starlinkEnterpriseElementIdArg);
            string userTerminalIdPk = args.GetArgumentValue(userTerminalDeviceIdArg);

            var dmsElementID = starlinkEnterpriseElement.Split('/');
            var dmaID = Convert.ToInt32(dmsElementID[0]);
            var elementID = Convert.ToInt32(dmsElementID[1]);

            var parameterPairs = new ParameterIndexPair[] { new ParameterIndexPair(1213, userTerminalIdPk), new ParameterIndexPair(1214, userTerminalIdPk) };
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

            GetTrendDataResponseMessage trendDataResponseMessage = _dms.SendMessage(trendMessage) as GetTrendDataResponseMessage;

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

        private DateTime RoundToNearest5Min(DateTime time)
        {
            int minutes = (time.Minute / 5) * 5;  // Always round down
            return new DateTime(time.Year, time.Month, time.Day, time.Hour, minutes, 0);
        }

        private void ProcessTrendResponseResult(GetTrendDataResponseMessage trendDataResponseMessage)
        {
            var latitudeData = new List<double>();

            foreach (var record in trendDataResponseMessage.Records)
            {
                if (!record.Value.Any())
                {
                    continue;
                }

                if (record.Key.StartsWith("1213")) // latitude
                {
                    foreach (var entry in record.Value)
                    {
                        if (entry is AverageTrendRecord avgTimeData && avgTimeData.Status == 5)
                        {
                            latitudeData.Add(avgTimeData.AverageValue);
                        }
                    }
                }
                else if (record.Key.StartsWith("1214")) // Longitude
                {
                    for (int i = 0; i < record.Value.Count; i++)
                    {
                        // sample the data 1 in 3.
                        if (i % 3 > 0)
                        {
                            continue;
                        }

                        if (record.Value[i] is AverageTrendRecord avgTimeData && avgTimeData.Status == 5)
                        {
                            double longitude = avgTimeData.AverageValue;
                            double latitude = latitudeData[i];

                            if (longitude == -1.0 || latitude == -1.0)
                            {
                                continue;
                            }

                            AddResultRow(i, avgTimeData.Time, latitude, longitude);
                        }
                    }
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
