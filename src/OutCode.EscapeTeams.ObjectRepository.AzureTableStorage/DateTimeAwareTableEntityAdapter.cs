using System;
using System.Collections.Generic;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;

namespace OutCode.EscapeTeams.ObjectRepository.AzureTableStorage
{
    internal class DateTimeAwareTableEntityAdapter<T> : TableEntityAdapter<T> where T:BaseEntity
    {
        public DateTimeAwareTableEntityAdapter()
        {
        }

        public DateTimeAwareTableEntityAdapter(T entity) : base(entity)
        {
            ETag = "*";
            RowKey = entity.Id.ToString();
            PartitionKey = entity.GetType().Name;
        }

        public override void ReadEntity(IDictionary<string, EntityProperty> properties,
            OperationContext operationContext)
        {
            base.ReadEntity(properties, operationContext);
            OriginalEntity.Id = Guid.Parse(RowKey);
        }
        
        public override IDictionary<string, EntityProperty> WriteEntity(OperationContext operationContext)
        {
            var result = base.WriteEntity(operationContext);

            foreach (var item in result)
            {
                if (item.Value.PropertyType == EdmType.DateTime && item.Value.DateTime == DateTime.MinValue)
                    item.Value.DateTime = null;
            }

            result.Remove(nameof(BaseEntity.Id));
            return result;
        }

        public bool InconsistentPartitionKey => PartitionKey != OriginalEntity.GetType().Name;
    }
}