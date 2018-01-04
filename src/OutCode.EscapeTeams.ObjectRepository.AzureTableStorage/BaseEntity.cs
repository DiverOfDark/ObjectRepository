using System;
using System.Collections.Generic;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;

namespace OutCode.EscapeTeams.ObjectRepository.AzureTableStorage
{
    public class BaseEntity : TableEntity
    {
        public override IDictionary<string, EntityProperty> WriteEntity(OperationContext operationContext)
        {
            var result = base.WriteEntity(operationContext);

            foreach (var item in result)
            {
                if (item.Value.PropertyType == EdmType.DateTime && item.Value.DateTime == DateTime.MinValue)
                    item.Value.DateTime = null;
            }
            return result;
        }

        public BaseEntity Touch()
        {
            ETag = "*";
            return this;
        }
    }
}