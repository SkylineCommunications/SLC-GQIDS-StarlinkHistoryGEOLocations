#pragma warning disable SA1633 // File header is missing

namespace DSStarlinkGeoHistoryLocations.Tests
#pragma warning restore IDE0079 // Remove unnecessary suppression
{
	[TestClass]
	public class DSStarlinkGeoHistoryLocationsTests
	{
		[TestMethod]
		[DataRow("2025-02-19T10:02:30", "2025-02-19T10:00:00")] // Rounds down to 10:00
		[DataRow("2025-02-19T10:04:59", "2025-02-19T10:00:00")] // Rounds down to 10:00
		[DataRow("2025-02-19T10:05:00", "2025-02-19T10:05:00")] // Stays at 10:05
		[DataRow("2025-02-19T10:06:01", "2025-02-19T10:05:00")] // Rounds down to 10:05
		[DataRow("2025-02-19T10:09:59", "2025-02-19T10:05:00")] // Rounds down to 10:05
		[DataRow("2025-02-19T10:10:00", "2025-02-19T10:10:00")] // Stays at 10:10
		[DataRow("2025-02-19T23:58:00", "2025-02-19T23:55:00")] // Rounds down to 23:55
		[DataRow("2025-02-19T00:01:00", "2025-02-19T00:00:00")] // Midnight edge case

		public void RoundToNearest5MinTest(string inputTime, string expectedTime)
		{
			// Arrange
			DateTime input = DateTime.Parse(inputTime);
			DateTime expected = DateTime.Parse(expectedTime);

			// Act
			DateTime result = DSStarlinkGeoHistoryLocations.RoundToNearest5Min(input);

			// Assert
			Assert.AreEqual(expected, result);
		}
	}
}