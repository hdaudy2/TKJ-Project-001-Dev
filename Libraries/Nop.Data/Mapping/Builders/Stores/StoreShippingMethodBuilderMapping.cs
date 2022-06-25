using FluentMigrator.Builders.Create.Table;
using Nop.Core.Domain.Stores;
using Nop.Core.Domain.Shipping;
using Nop.Data.Extensions;

namespace Nop.Data.Mapping.Builders.Stores
{
    /// <summary>
    /// Represents a Store Shipping Method Mapping entity builder
    /// </summary>
    public partial class StoreShippingMethodBuilder : NopEntityBuilder<StoreShippingMethod>
    {
        #region Methods

        /// <summary>
        /// Apply entity configuration
        /// </summary>
        /// <param name="table">Create table expression builder</param>
        public override void MapEntity(CreateTableExpressionBuilder table)
        {
            table
                .WithColumn(NameCompatibilityManager.GetColumnName(typeof(StoreShippingMethod), nameof(StoreShippingMethod.ShippingMethodId)))
                    .AsInt32().ForeignKey<ShippingMethod>().Nullable()
                .WithColumn(NameCompatibilityManager.GetColumnName(typeof(StoreShippingMethod), nameof(StoreShippingMethod.StoreId)))
                    .AsInt32().ForeignKey<Store>().Nullable();
        }

        #endregion
    }
}