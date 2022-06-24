using Nop.Core.Domain.Localization;

namespace Nop.Core.Domain.Stores
{
    /// <summary>
    /// Represents a store shipping method record
    /// </summary>
    public partial class StoreShippingMethod : BaseEntity, ILocalizedEntity
    {
        /// <summary>
        /// Gets or sets the entity StoreId reference to Store id
        /// </summary>
        public int StoreId { get; set; }

        /// <summary>
        /// Gets or sets the entity ShippingMethodId reference to ShippingMethod id
        /// </summary>
        public int ShippingMethodId { get; set; }
    }
}
