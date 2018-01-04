using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Table;

namespace OutCode.EscapeTeams.ObjectRepository.AzureTableStorage
{
    public static class AzureExtensions
    {
        public static async Task<List<IListBlobItem>> ListBlobsAsync(this CloudBlobContainer container)
        {
            var realResult = new List<IListBlobItem>();

            var result = await container.ListBlobsSegmentedAsync(null, true, BlobListingDetails.All, null, null, null, null);
            realResult.AddRange(result.Results);
            while (result.ContinuationToken != null)
            {
                result = await container.ListBlobsSegmentedAsync(null, true, BlobListingDetails.All, null, result.ContinuationToken, null, null);
                realResult.AddRange(result.Results);
            }

            return realResult;
        }

        public static async Task<List<T>> ExecuteQueryAsync<T>(this CloudTable table, TableQuery<T> query) where T : ITableEntity, new()
        {
            var realResult = new List<T>();

            var result = await table.ExecuteQuerySegmentedAsync(query, null);
            realResult.AddRange(result.Results);
            while (result.ContinuationToken != null)
            {
                result = await table.ExecuteQuerySegmentedAsync(query, result.ContinuationToken);
                realResult.AddRange(result.Results);
            }

            return realResult;
        }
    }
}