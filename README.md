# Starlink - Get History GEO Locations - AVG.5M

## Overview  
This script processes **trend data records** containing latitude and longitude values (5M average trend results), ensuring they are correctly aligned based on timestamps. The goal is to:  
- **Match latitude and longitude data** based on a rounded time window (nearest 5 minutes).  
- **Dynamically sample** longitude data to ensure a maximum of **1000 records** for efficiency.  

## Features  
✔ **Time-based matching:** Ensures latitude and longitude values align even when timestamps differ slightly.  
✔ **Dynamic sampling rate:** Adjusts to the number of records to limit output to **1000 records or fewer**.  
✔ **Efficient processing:** Uses dictionaries for quick lookups and minimal looping.  

## How It Works  
1. **Latitude and longitude values** are extracted from `trendDataResponseMessage.Records`.  
2. **Timestamps are rounded** down to the nearest 5-minute mark for consistency.  
3. **A dictionary is used** to store values, ensuring only matching time windows are processed.  
4. **Sampling rate is dynamically determined** based on the number of longitude records:  
   ```csharp
   int samplingRate = (int)Math.Ceiling((double)record.Value.Count / 1000);
