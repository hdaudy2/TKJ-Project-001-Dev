using System;
using Nop.Core;
using System.Collections.Generic;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Customers;
using Nop.Services.ExportImport.Help;

namespace Nop.Services.ExportImport
{
    public class ImportRolesColumnMetadata
    {
        public PropertyManager<TierPriceRoleImport> Manager { get; internal set; }
        public IList<PropertyByName<TierPriceRoleImport>> Properties { get; set; }
        public IList<RoleDetail> RoleDetails { get; set; }
    }

    public class ImportTierPriceMetadata
    {
        public PropertyManager<TierPriceImport> Manager { get; internal set; }
        public IList<PropertyByName<TierPriceImport>> Properties { get; set; }
        public IList<Product> ProductList { get; set; }
        public IList<TierPriceImport> ImportTierPriceList { get; set; }
        public int ProductsInFile { get; set; }
    }

    public partial class TierPriceRoleImport : BaseEntity
    {
        public string RoleName { get; set; }
        public string ColumnName { get; set; }
    }

    #region Nested Classes

    public partial class TierPriceImport : BaseEntity
    {
        public string Name { get; set; }
        public string SKU { get; set; }
        public decimal ProductPrice { get; set; }
        public string RoleForTireOne { get; set; }
        public string RoleForTireTwo { get; set; }
        public string RoleForTireThree { get; set; }
        public string RoleForTireFour { get; set; }
        public string RoleForTireFive { get; set; }
        public string RoleForTireSix { get; set; }
        public double PriceForTireOne { get; set; }
        public double PriceForTireTwo { get; set; }
        public double PriceForTireThree { get; set; }
        public double PriceForTireFour { get; set; }
        public double PriceForTireFive { get; set; }
        public double PriceForTireSix { get; set; }
    }

    public partial class RoleDetail
    {
        public RoleDetail(string Role, string Column)
        {
            this.Role = Role;
            this.Column = Column;
        }
        public string Role { get; set; }
        public string Column { get; set; }
    }

    public partial class TirePriceListObject
    {
        public TirePriceListObject(double Price, string Role)
        {
            this.Price = Price;
            this.Role = Role;
        }

        public double Price { get; set; }
        public string Role { get; set; }
    }

    #endregion
}
