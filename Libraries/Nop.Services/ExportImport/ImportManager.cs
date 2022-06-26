﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.DependencyInjection;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Media;
using Nop.Core.Domain.Messages;
using Nop.Core.Domain.Shipping;
using Nop.Core.Domain.Tax;
using Nop.Core.Domain.Vendors;
using Nop.Core.Http;
using Nop.Core.Infrastructure;
using Nop.Data;
using Nop.Services.Catalog;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.ExportImport.Help;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Media;
using Nop.Services.Messages;
using Nop.Services.Seo;
using Nop.Services.Shipping;
using Nop.Services.Shipping.Date;
using Nop.Services.Stores;
using Nop.Services.Tax;
using Nop.Services.Vendors;

namespace Nop.Services.ExportImport
{
    /// <summary>
    /// Import manager
    /// </summary>
    public partial class ImportManager : IImportManager
    {
        #region Fields

        private readonly CatalogSettings _catalogSettings;
        private readonly ICategoryService _categoryService;
        private readonly ICountryService _countryService;
        private readonly ICustomerService _customerService;
        private readonly ICustomerActivityService _customerActivityService;
        private readonly INopDataProvider _dataProvider;
        private readonly IDateRangeService _dateRangeService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILocalizationService _localizationService;
        private readonly ILogger _logger;
        private readonly IManufacturerService _manufacturerService;
        private readonly IMeasureService _measureService;
        private readonly INewsLetterSubscriptionService _newsLetterSubscriptionService;
        private readonly INopFileProvider _fileProvider;
        private readonly IPictureService _pictureService;
        private readonly IProductAttributeService _productAttributeService;
        private readonly IProductService _productService;
        private readonly IProductTagService _productTagService;
        private readonly IProductTemplateService _productTemplateService;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly IShippingService _shippingService;
        private readonly ISpecificationAttributeService _specificationAttributeService;
        private readonly IStateProvinceService _stateProvinceService;
        private readonly IStoreContext _storeContext;
        private readonly IStoreMappingService _storeMappingService;
        private readonly IStoreService _storeService;
        private readonly ITaxCategoryService _taxCategoryService;
        private readonly IUrlRecordService _urlRecordService;
        private readonly IVendorService _vendorService;
        private readonly IWorkContext _workContext;
        private readonly MediaSettings _mediaSettings;
        private readonly VendorSettings _vendorSettings;

        #endregion

        #region Ctor

        public ImportManager(CatalogSettings catalogSettings,
            ICategoryService categoryService,
            ICountryService countryService,
            ICustomerService customerService,
            ICustomerActivityService customerActivityService,
            INopDataProvider dataProvider,
            IDateRangeService dateRangeService,
            IHttpClientFactory httpClientFactory,
            ILocalizationService localizationService,
            ILogger logger,
            IManufacturerService manufacturerService,
            IMeasureService measureService,
            INewsLetterSubscriptionService newsLetterSubscriptionService,
            INopFileProvider fileProvider,
            IPictureService pictureService,
            IProductAttributeService productAttributeService,
            IProductService productService,
            IProductTagService productTagService,
            IProductTemplateService productTemplateService,
            IServiceScopeFactory serviceScopeFactory,
            IShippingService shippingService,
            ISpecificationAttributeService specificationAttributeService,
            IStateProvinceService stateProvinceService,
            IStoreContext storeContext,
            IStoreMappingService storeMappingService,
            IStoreService storeService,
            ITaxCategoryService taxCategoryService,
            IUrlRecordService urlRecordService,
            IVendorService vendorService,
            IWorkContext workContext,
            MediaSettings mediaSettings,
            VendorSettings vendorSettings)
        {
            _catalogSettings = catalogSettings;
            _categoryService = categoryService;
            _countryService = countryService;
            _customerService = customerService;
            _customerActivityService = customerActivityService;
            _dataProvider = dataProvider;
            _dateRangeService = dateRangeService;
            _httpClientFactory = httpClientFactory;
            _fileProvider = fileProvider;
            _localizationService = localizationService;
            _logger = logger;
            _manufacturerService = manufacturerService;
            _measureService = measureService;
            _newsLetterSubscriptionService = newsLetterSubscriptionService;
            _pictureService = pictureService;
            _productAttributeService = productAttributeService;
            _productService = productService;
            _productTagService = productTagService;
            _productTemplateService = productTemplateService;
            _serviceScopeFactory = serviceScopeFactory;
            _shippingService = shippingService;
            _specificationAttributeService = specificationAttributeService;
            _stateProvinceService = stateProvinceService;
            _storeContext = storeContext;
            _storeMappingService = storeMappingService;
            _storeService = storeService;
            _taxCategoryService = taxCategoryService;
            _urlRecordService = urlRecordService;
            _vendorService = vendorService;
            _workContext = workContext;
            _mediaSettings = mediaSettings;
            _vendorSettings = vendorSettings;
        }

        #endregion

        #region Utilities

        private static ExportedAttributeType GetTypeOfExportedAttribute(IXLWorksheet worksheet, PropertyManager<ExportProductAttribute> productAttributeManager, PropertyManager<ExportSpecificationAttribute> specificationAttributeManager, int iRow)
        {
            productAttributeManager.ReadFromXlsx(worksheet, iRow, ExportProductAttribute.ProducAttributeCellOffset);

            if (productAttributeManager.IsCaption)
            {
                return ExportedAttributeType.ProductAttribute;
            }

            specificationAttributeManager.ReadFromXlsx(worksheet, iRow, ExportProductAttribute.ProducAttributeCellOffset);

            if (specificationAttributeManager.IsCaption)
            {
                return ExportedAttributeType.SpecificationAttribute;
            }

            return ExportedAttributeType.NotSpecified;
        }

        /// <returns>A task that represents the asynchronous operation</returns>
        private static async Task SetOutLineForSpecificationAttributeRowAsync(object cellValue, IXLWorksheet worksheet, int endRow)
        {
            var attributeType = (cellValue ?? string.Empty).ToString();

            if (attributeType.Equals("AttributeType", StringComparison.InvariantCultureIgnoreCase))
            {
                worksheet.Row(endRow).OutlineLevel = 1;
            }
            else
            {
                if ((await SpecificationAttributeType.Option.ToSelectListAsync(useLocalization: false))
                    .Any(p => p.Text.Equals(attributeType, StringComparison.InvariantCultureIgnoreCase)))
                    worksheet.Row(endRow).OutlineLevel = 1;
                else if (int.TryParse(attributeType, out var attributeTypeId) && Enum.IsDefined(typeof(SpecificationAttributeType), attributeTypeId))
                    worksheet.Row(endRow).OutlineLevel = 1;
            }
        }

        private static void CopyDataToNewFile(ImportProductMetadata metadata, IXLWorksheet worksheet, string filePath, int startRow, int endRow, int endCell)
        {
            using var stream = new FileStream(filePath, FileMode.OpenOrCreate);
            // ok, we can run the real code of the sample now
            using var workbook = new XLWorkbook(stream);
            // uncomment this line if you want the XML written out to the outputDir
            //xlPackage.DebugMode = true; 

            // get handles to the worksheets
            var outWorksheet = workbook.Worksheets.Add(typeof(Product).Name);
            metadata.Manager.WriteCaption(outWorksheet);
            var outRow = 2;
            for (var row = startRow; row <= endRow; row++)
            {
                outWorksheet.Row(outRow).OutlineLevel = worksheet.Row(row).OutlineLevel;
                for (var cell = 1; cell <= endCell; cell++)
                {
                    outWorksheet.Row(outRow).Cell(cell).Value = worksheet.Row(row).Cell(cell).Value;
                }

                outRow += 1;
            }

            workbook.Save();
        }

        protected virtual int GetColumnIndex(string[] properties, string columnName)
        {
            if (properties == null)
                throw new ArgumentNullException(nameof(properties));

            if (columnName == null)
                throw new ArgumentNullException(nameof(columnName));

            for (var i = 0; i < properties.Length; i++)
                if (properties[i].Equals(columnName, StringComparison.InvariantCultureIgnoreCase))
                    return i + 1; //excel indexes start from 1
            return 0;
        }

        protected virtual string GetMimeTypeFromFilePath(string filePath)
        {
            new FileExtensionContentTypeProvider().TryGetContentType(filePath, out var mimeType);

            //set to jpeg in case mime type cannot be found
            return mimeType ?? MimeTypes.ImageJpeg;
        }

        /// <summary>
        /// Creates or loads the image
        /// </summary>
        /// <param name="picturePath">The path to the image file</param>
        /// <param name="name">The name of the object</param>
        /// <param name="picId">Image identifier, may be null</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the image or null if the image has not changed
        /// </returns>
        protected virtual async Task<Picture> LoadPictureAsync(string picturePath, string name, int? picId = null)
        {
            if (string.IsNullOrEmpty(picturePath) || !_fileProvider.FileExists(picturePath))
                return null;

            var mimeType = GetMimeTypeFromFilePath(picturePath);
            var newPictureBinary = await _fileProvider.ReadAllBytesAsync(picturePath);
            var pictureAlreadyExists = false;
            if (picId != null)
            {
                //compare with existing product pictures
                var existingPicture = await _pictureService.GetPictureByIdAsync(picId.Value);
                if (existingPicture != null)
                {
                    var existingBinary = await _pictureService.LoadPictureBinaryAsync(existingPicture);
                    //picture binary after validation (like in database)
                    var validatedPictureBinary = await _pictureService.ValidatePictureAsync(newPictureBinary, mimeType);
                    if (existingBinary.SequenceEqual(validatedPictureBinary) ||
                        existingBinary.SequenceEqual(newPictureBinary))
                    {
                        pictureAlreadyExists = true;
                    }
                }
            }

            if (pictureAlreadyExists)
                return null;

            var newPicture = await _pictureService.InsertPictureAsync(newPictureBinary, mimeType, await _pictureService.GetPictureSeNameAsync(name));
            return newPicture;
        }

        /// <returns>A task that represents the asynchronous operation</returns>
        private async Task LogPictureInsertErrorAsync(string picturePath, Exception ex)
        {
            var extension = _fileProvider.GetFileExtension(picturePath);
            var name = _fileProvider.GetFileNameWithoutExtension(picturePath);

            var point = string.IsNullOrEmpty(extension) ? string.Empty : ".";
            var fileName = _fileProvider.FileExists(picturePath) ? $"{name}{point}{extension}" : string.Empty;
            
            await _logger.ErrorAsync($"Insert picture failed (file name: {fileName})", ex);
        }

        /// <returns>A task that represents the asynchronous operation</returns>
        protected virtual async Task ImportProductImagesUsingServicesAsync(IList<ProductPictureMetadata> productPictureMetadata)
        {
            foreach (var product in productPictureMetadata)
            {
                foreach (var picturePath in new[] { product.Picture1Path, product.Picture2Path, product.Picture3Path })
                {
                    if (string.IsNullOrEmpty(picturePath))
                        continue;

                    var mimeType = GetMimeTypeFromFilePath(picturePath);
                    var newPictureBinary = await _fileProvider.ReadAllBytesAsync(picturePath);
                    var pictureAlreadyExists = false;
                    if (!product.IsNew)
                    {
                        //compare with existing product pictures
                        var existingPictures = await _pictureService.GetPicturesByProductIdAsync(product.ProductItem.Id);
                        foreach (var existingPicture in existingPictures)
                        {
                            var existingBinary = await _pictureService.LoadPictureBinaryAsync(existingPicture);
                            //picture binary after validation (like in database)
                            var validatedPictureBinary = await _pictureService.ValidatePictureAsync(newPictureBinary, mimeType);
                            if (!existingBinary.SequenceEqual(validatedPictureBinary) &&
                                !existingBinary.SequenceEqual(newPictureBinary))
                                continue;
                            //the same picture content
                            pictureAlreadyExists = true;
                            break;
                        }
                    }

                    if (pictureAlreadyExists)
                        continue;

                    try
                    {
                        var newPicture = await _pictureService.InsertPictureAsync(newPictureBinary, mimeType, await _pictureService.GetPictureSeNameAsync(product.ProductItem.Name));
                        await _productService.InsertProductPictureAsync(new ProductPicture
                        {
                            //EF has some weird issue if we set "Picture = newPicture" instead of "PictureId = newPicture.Id"
                            //pictures are duplicated
                            //maybe because entity size is too large
                            PictureId = newPicture.Id,
                            DisplayOrder = 1,
                            ProductId = product.ProductItem.Id
                        });
                        await _productService.UpdateProductAsync(product.ProductItem);
                    }
                    catch (Exception ex)
                    {
                        await LogPictureInsertErrorAsync(picturePath, ex);
                    }
                }
            }
        }

        /// <returns>A task that represents the asynchronous operation</returns>
        protected virtual async Task ImportProductImagesUsingHashAsync(IList<ProductPictureMetadata> productPictureMetadata, IList<Product> allProductsBySku)
        {
            //performance optimization, load all pictures hashes
            //it will only be used if the images are stored in the SQL Server database (not compact)
            var trimByteCount = _dataProvider.SupportedLengthOfBinaryHash - 1;
            var productsImagesIds = await _productService.GetProductsImagesIdsAsync(allProductsBySku.Select(p => p.Id).ToArray());

            var allProductPictureIds = productsImagesIds.SelectMany(p => p.Value);

            var allPicturesHashes = allProductPictureIds.Any() ? await _dataProvider.GetFieldHashesAsync<PictureBinary>(p => allProductPictureIds.Contains(p.PictureId), 
                p => p.PictureId, p => p.BinaryData) : new Dictionary<int, string>();

            foreach (var product in productPictureMetadata)
            {
                foreach (var picturePath in new[] { product.Picture1Path, product.Picture2Path, product.Picture3Path })
                {
                    if (string.IsNullOrEmpty(picturePath))
                        continue;
                    try
                    {
                        var mimeType = GetMimeTypeFromFilePath(picturePath);
                        var newPictureBinary = await _fileProvider.ReadAllBytesAsync(picturePath);
                        var pictureAlreadyExists = false;
                        if (!product.IsNew)
                        {
                            var newImageHash = HashHelper.CreateHash(
                                newPictureBinary,
                                ExportImportDefaults.ImageHashAlgorithm,
                                trimByteCount);

                            var newValidatedImageHash = HashHelper.CreateHash(
                                await _pictureService.ValidatePictureAsync(newPictureBinary, mimeType),
                                ExportImportDefaults.ImageHashAlgorithm,
                                trimByteCount);

                            var imagesIds = productsImagesIds.ContainsKey(product.ProductItem.Id)
                                ? productsImagesIds[product.ProductItem.Id]
                                : Array.Empty<int>();

                            pictureAlreadyExists = allPicturesHashes.Where(p => imagesIds.Contains(p.Key))
                                .Select(p => p.Value)
                                .Any(p => 
                                    p.Equals(newImageHash, StringComparison.OrdinalIgnoreCase) || 
                                    p.Equals(newValidatedImageHash, StringComparison.OrdinalIgnoreCase));
                        }

                        if (pictureAlreadyExists)
                            continue;

                        var newPicture = await _pictureService.InsertPictureAsync(newPictureBinary, mimeType, await _pictureService.GetPictureSeNameAsync(product.ProductItem.Name));

                        await _productService.InsertProductPictureAsync(new ProductPicture
                        {
                            //EF has some weird issue if we set "Picture = newPicture" instead of "PictureId = newPicture.Id"
                            //pictures are duplicated
                            //maybe because entity size is too large
                            PictureId = newPicture.Id,
                            DisplayOrder = 1,
                            ProductId = product.ProductItem.Id
                        });

                        await _productService.UpdateProductAsync(product.ProductItem);
                    }
                    catch (Exception ex)
                    {
                        await LogPictureInsertErrorAsync(picturePath, ex);
                    }
                }
            }
        }

        /// <returns>A task that represents the asynchronous operation</returns>
        protected virtual async Task<(string seName, bool isParentCategoryExists)> UpdateCategoryByXlsxAsync(Category category, PropertyManager<Category> manager, Dictionary<string, ValueTask<Category>> allCategories, bool isNew)
        {
            var seName = string.Empty;
            var isParentCategoryExists = true;
            var isParentCategorySet = false;

            foreach (var property in manager.GetProperties)
            {
                switch (property.PropertyName)
                {
                    case "Name":
                    case "Title en":
                        category.Name = property.StringValue.Split(new[] { ">>" }, StringSplitOptions.RemoveEmptyEntries).Last().Trim();
                        seName = await _urlRecordService.GetSeNameAsync(category.Name, true, false);
                        break;
                    case "Description":
                    case "Description Top en":
                        category.Description = property.StringValue;
                        break;
                    case "CategoryTemplateId":
                        category.CategoryTemplateId = property.IntValue;
                        break;
                    case "Code":
                    // category.CodeId = int.Parse(string.Join("", property.StringValue.Split('-')));
                    category.CodeId = property.StringValue;
                    break;
                    case "MetaKeywords":
                    case "Description MetaKeywords en":
                        category.MetaKeywords = property.StringValue;
                        break;
                    case "MetaDescription":
                    case "Description MetaDescription en":
                        category.MetaDescription = property.StringValue;
                        break;
                    case "MetaTitle":
                    case "Description PageTitle en":
                        category.MetaTitle = property.StringValue;
                        break;
                    case "ParentCategoryId":
                        if (!isParentCategorySet)
                        {
                            var parentCategory = await await allCategories.Values.FirstOrDefaultAwaitAsync(async c => (await c).Id == property.IntValue);
                            isParentCategorySet = parentCategory != null;

                            isParentCategoryExists = isParentCategorySet || property.IntValue == 0;

                            category.ParentCategoryId = parentCategory?.Id ?? property.IntValue;
                        }

                        break;
                    case "CodeParent":
                        if (!isParentCategorySet)
                        {

                            var value = property.StringValue == "PG" ? "0" : property.StringValue;

                            var parentCategory = await await allCategories.Values.FirstOrDefaultAwaitAsync(async c => (await c).CodeId == value);
                            isParentCategorySet = parentCategory != null;

                            isParentCategoryExists = isParentCategorySet || value == "0";

                            category.ParentCategoryId = parentCategory?.Id ?? 0;
                        }

                        break;
                    case "ParentCategoryName":
                        if (_catalogSettings.ExportImportCategoriesUsingCategoryName && !isParentCategorySet)
                        {
                            var categoryName = manager.GetProperty("ParentCategoryName").StringValue;
                            if (!string.IsNullOrEmpty(categoryName))
                            {
                                var parentCategory = allCategories.ContainsKey(categoryName)
                                    //try find category by full name with all parent category names
                                    ? await allCategories[categoryName]
                                    //try find category by name
                                    : await await allCategories.Values.FirstOrDefaultAwaitAsync(async c => (await c).Name.Equals(categoryName, StringComparison.InvariantCulture));

                                if (parentCategory != null)
                                {
                                    category.ParentCategoryId = parentCategory.Id;
                                    isParentCategorySet = true;
                                }
                                else
                                {
                                    isParentCategoryExists = false;
                                }
                            }
                        }

                        break;
                    case "Picture":
                        var picture = await LoadPictureAsync(manager.GetProperty("Picture").StringValue, category.Name, isNew ? null : (int?)category.PictureId);
                        if (picture != null)
                            category.PictureId = picture.Id;
                        break;
                    case "PageSize":
                        category.PageSize = property.IntValue;
                        break;
                    case "AllowCustomersToSelectPageSize":
                        category.AllowCustomersToSelectPageSize = property.BooleanValue;
                        break;
                    case "PageSizeOptions":
                        category.PageSizeOptions = property.StringValue;
                        break;
                    case "ShowOnHomepage":
                        category.ShowOnHomepage = property.BooleanValue;
                        break;
                    case "PriceRangeFiltering":
                        category.PriceRangeFiltering = property.BooleanValue;
                        break;
                    case "PriceFrom":
                        category.PriceFrom = property.DecimalValue;
                        break;
                    case "PriceTo":
                        category.PriceTo = property.DecimalValue;
                        break;
                    case "AutomaticallyCalculatePriceRange":
                        category.ManuallyPriceRange = property.BooleanValue;
                        break;
                    case "IncludeInTopMenu":
                        category.IncludeInTopMenu = property.BooleanValue;
                        break;
                    case "Published":
                        category.Published = property.BooleanValue;
                        break;
                    case "Status":
                        category.Published = property.StringValue == "Enable";
                        break;
                    case "ShowNavigation":
                        category.IncludeInTopMenu = property.StringValue == "yes";
                        break;
                    case "DisplayOrder":
                        category.DisplayOrder = property.IntValue;
                        break;
                    case "Order":
                        category.DisplayOrder = property.IntValue - 1;
                        break;
                    case "SeName":
                        seName = property.StringValue;
                        break;
                }
            }

            category.UpdatedOnUtc = DateTime.UtcNow;
            return (seName, isParentCategoryExists);
        }

        /// <returns>A task that represents the asynchronous operation</returns>
        protected virtual async Task<(Category category, bool isNew, string curentCategoryBreadCrumb)> GetCategoryFromXlsxAsync(PropertyManager<Category> manager, IXLWorksheet worksheet, int iRow, Dictionary<string, ValueTask<Category>> allCategories)
        {
            manager.ReadFromXlsx(worksheet, iRow);

            //try get category from database by ID
            var category = await await allCategories.Values.FirstOrDefaultAwaitAsync(async c => (await c).Id == manager.GetProperty("Id")?.IntValue);

            if (_catalogSettings.ExportImportCategoriesUsingCategoryName && category == null)
            {
                var categoryName = manager.GetProperty("Name").StringValue;
                if (!string.IsNullOrEmpty(categoryName))
                {
                    category = allCategories.ContainsKey(categoryName)
                        //try find category by full name with all parent category names
                        ? await allCategories[categoryName]
                        //try find category by name
                        : await await allCategories.Values.FirstOrDefaultAwaitAsync(async c => (await c).Name.Equals(categoryName, StringComparison.InvariantCulture));
                }
            }

            var isNew = category == null;

            category ??= new Category();

            var curentCategoryBreadCrumb = string.Empty;

            if (isNew)
            {
                category.CreatedOnUtc = DateTime.UtcNow;
                //default values
                category.PageSize = _catalogSettings.DefaultCategoryPageSize;
                category.PageSizeOptions = _catalogSettings.DefaultCategoryPageSizeOptions;
                category.Published = true;
                category.IncludeInTopMenu = true;
                category.AllowCustomersToSelectPageSize = true;
            }
            else
                curentCategoryBreadCrumb = await _categoryService.GetFormattedBreadCrumbAsync(category);

            return (category, isNew, curentCategoryBreadCrumb);
        }

        /// <returns>A task that represents the asynchronous operation</returns>
        protected virtual async Task SaveCategoryAsync(bool isNew, Category category, Dictionary<string, ValueTask<Category>> allCategories, string curentCategoryBreadCrumb, bool setSeName, string seName)
        {
            var customer = await _workContext.GetCurrentCustomerAsync();
            var IsLimitedToStores = !await _customerService.IsAdminAsync(customer);

            #region Multi-Tenant Plugin
            var _storeMappingService = Nop.Core.Infrastructure.EngineContext.Current.Resolve<Nop.Services.Stores.IStoreMappingService>();
            #endregion

            if (isNew){
                #region Multi-Tenant Plugin
                category.LimitedToStores = IsLimitedToStores;
                #endregion
                
                await _categoryService.InsertCategoryAsync(category);
                
                #region Multi-Tenant Plugin
                if (await _storeMappingService.CurrentStore() > 0)
                {
                    await _storeMappingService.InsertStoreMappingAsync(category, await _storeMappingService.CurrentStore());
                }
                #endregion
            }
            else
                await _categoryService.UpdateCategoryAsync(category);

            var categoryBreadCrumb = await _categoryService.GetFormattedBreadCrumbAsync(category);
            if (!allCategories.ContainsKey(categoryBreadCrumb))
                allCategories.Add(categoryBreadCrumb, new ValueTask<Category>(category));
            if (!string.IsNullOrEmpty(curentCategoryBreadCrumb) && allCategories.ContainsKey(curentCategoryBreadCrumb) &&
                categoryBreadCrumb != curentCategoryBreadCrumb)
                allCategories.Remove(curentCategoryBreadCrumb);

            //search engine name
            if (setSeName)
                await _urlRecordService.SaveSlugAsync(category, await _urlRecordService.ValidateSeNameAsync(category, seName, category.Name, true), 0);
        }

        /// <returns>A task that represents the asynchronous operation</returns>
        protected virtual async Task SetOutLineForProductAttributeRowAsync(object cellValue, IXLWorksheet worksheet, int endRow)
        {
            try
            {
                var aid = Convert.ToInt32(cellValue ?? -1);

                var productAttribute = await _productAttributeService.GetProductAttributeByIdAsync(aid);

                if (productAttribute != null)
                    worksheet.Row(endRow).OutlineLevel = 1;
            }
            catch (FormatException)
            {
                if ((cellValue ?? string.Empty).ToString() == "AttributeId")
                    worksheet.Row(endRow).OutlineLevel = 1;
            }
        }

        /// <returns>A task that represents the asynchronous operation</returns>
        protected virtual async Task ImportProductAttributeAsync(PropertyManager<ExportProductAttribute> productAttributeManager, Product lastLoadedProduct)
        {
            if (!_catalogSettings.ExportImportProductAttributes || lastLoadedProduct == null || productAttributeManager.IsCaption)
                return;

            var productAttributeId = productAttributeManager.GetProperty("AttributeId").IntValue;
            var attributeControlTypeId = productAttributeManager.GetProperty("AttributeControlType").IntValue;

            var productAttributeValueId = productAttributeManager.GetProperty("ProductAttributeValueId").IntValue;
            var associatedProductId = productAttributeManager.GetProperty("AssociatedProductId").IntValue;
            var valueName = productAttributeManager.GetProperty("ValueName").StringValue;
            var attributeValueTypeId = productAttributeManager.GetProperty("AttributeValueType").IntValue;
            var colorSquaresRgb = productAttributeManager.GetProperty("ColorSquaresRgb").StringValue;
            var imageSquaresPictureId = productAttributeManager.GetProperty("ImageSquaresPictureId").IntValue;
            var priceAdjustment = productAttributeManager.GetProperty("PriceAdjustment").DecimalValue;
            var priceAdjustmentUsePercentage = productAttributeManager.GetProperty("PriceAdjustmentUsePercentage").BooleanValue;
            var weightAdjustment = productAttributeManager.GetProperty("WeightAdjustment").DecimalValue;
            var cost = productAttributeManager.GetProperty("Cost").DecimalValue;
            var customerEntersQty = productAttributeManager.GetProperty("CustomerEntersQty").BooleanValue;
            var quantity = productAttributeManager.GetProperty("Quantity").IntValue;
            var isPreSelected = productAttributeManager.GetProperty("IsPreSelected").BooleanValue;
            var displayOrder = productAttributeManager.GetProperty("DisplayOrder").IntValue;
            var pictureId = productAttributeManager.GetProperty("PictureId").IntValue;
            var textPrompt = productAttributeManager.GetProperty("AttributeTextPrompt").StringValue;
            var isRequired = productAttributeManager.GetProperty("AttributeIsRequired").BooleanValue;
            var attributeDisplayOrder = productAttributeManager.GetProperty("AttributeDisplayOrder").IntValue;

            var productAttributeMapping = (await _productAttributeService.GetProductAttributeMappingsByProductIdAsync(lastLoadedProduct.Id))
                .FirstOrDefault(pam => pam.ProductAttributeId == productAttributeId);

            if (productAttributeMapping == null)
            {
                //insert mapping
                productAttributeMapping = new ProductAttributeMapping
                {
                    ProductId = lastLoadedProduct.Id,
                    ProductAttributeId = productAttributeId,
                    TextPrompt = textPrompt,
                    IsRequired = isRequired,
                    AttributeControlTypeId = attributeControlTypeId,
                    DisplayOrder = attributeDisplayOrder
                };
                await _productAttributeService.InsertProductAttributeMappingAsync(productAttributeMapping);
            }
            else
            {
                productAttributeMapping.AttributeControlTypeId = attributeControlTypeId;
                productAttributeMapping.TextPrompt = textPrompt;
                productAttributeMapping.IsRequired = isRequired;
                productAttributeMapping.DisplayOrder = attributeDisplayOrder;
                await _productAttributeService.UpdateProductAttributeMappingAsync(productAttributeMapping);
            }

            var pav = (await _productAttributeService.GetProductAttributeValuesAsync(productAttributeMapping.Id))
                .FirstOrDefault(p => p.Id == productAttributeValueId);

            //var pav = await _productAttributeService.GetProductAttributeValueByIdAsync(productAttributeValueId);

            var attributeControlType = (AttributeControlType)attributeControlTypeId;

            if (pav == null)
            {
                switch (attributeControlType)
                {
                    case AttributeControlType.Datepicker:
                    case AttributeControlType.FileUpload:
                    case AttributeControlType.MultilineTextbox:
                    case AttributeControlType.TextBox:
                        if (productAttributeMapping.ValidationRulesAllowed())
                        {
                            productAttributeMapping.ValidationMinLength = productAttributeManager.GetProperty("ValidationMinLength")?.IntValueNullable;
                            productAttributeMapping.ValidationMaxLength = productAttributeManager.GetProperty("ValidationMaxLength")?.IntValueNullable;
                            productAttributeMapping.ValidationFileMaximumSize = productAttributeManager.GetProperty("ValidationFileMaximumSize")?.IntValueNullable;
                            productAttributeMapping.ValidationFileAllowedExtensions = productAttributeManager.GetProperty("ValidationFileAllowedExtensions")?.StringValue;
                            productAttributeMapping.DefaultValue = productAttributeManager.GetProperty("DefaultValue")?.StringValue;

                            await _productAttributeService.UpdateProductAttributeMappingAsync(productAttributeMapping);
                        }

                        return;
                }

                pav = new ProductAttributeValue
                {
                    ProductAttributeMappingId = productAttributeMapping.Id,
                    AttributeValueType = (AttributeValueType)attributeValueTypeId,
                    AssociatedProductId = associatedProductId,
                    Name = valueName,
                    PriceAdjustment = priceAdjustment,
                    PriceAdjustmentUsePercentage = priceAdjustmentUsePercentage,
                    WeightAdjustment = weightAdjustment,
                    Cost = cost,
                    IsPreSelected = isPreSelected,
                    DisplayOrder = displayOrder,
                    ColorSquaresRgb = colorSquaresRgb,
                    ImageSquaresPictureId = imageSquaresPictureId,
                    CustomerEntersQty = customerEntersQty,
                    Quantity = quantity,
                    PictureId = pictureId
                };

                await _productAttributeService.InsertProductAttributeValueAsync(pav);
            }
            else
            {
                pav.AttributeValueTypeId = attributeValueTypeId;
                pav.AssociatedProductId = associatedProductId;
                pav.Name = valueName;
                pav.ColorSquaresRgb = colorSquaresRgb;
                pav.ImageSquaresPictureId = imageSquaresPictureId;
                pav.PriceAdjustment = priceAdjustment;
                pav.PriceAdjustmentUsePercentage = priceAdjustmentUsePercentage;
                pav.WeightAdjustment = weightAdjustment;
                pav.Cost = cost;
                pav.CustomerEntersQty = customerEntersQty;
                pav.Quantity = quantity;
                pav.IsPreSelected = isPreSelected;
                pav.DisplayOrder = displayOrder;
                pav.PictureId = pictureId;

                await _productAttributeService.UpdateProductAttributeValueAsync(pav);
            }
        }

        /// <returns>A task that represents the asynchronous operation</returns>
        private async Task ImportSpecificationAttributeAsync(PropertyManager<ExportSpecificationAttribute> specificationAttributeManager, Product lastLoadedProduct)
        {
            if (!_catalogSettings.ExportImportProductSpecificationAttributes || lastLoadedProduct == null || specificationAttributeManager.IsCaption)
                return;

            var attributeTypeId = specificationAttributeManager.GetProperty("AttributeType").IntValue;
            var allowFiltering = specificationAttributeManager.GetProperty("AllowFiltering").BooleanValue;
            var specificationAttributeOptionId = specificationAttributeManager.GetProperty("SpecificationAttributeOptionId").IntValue;
            var productId = lastLoadedProduct.Id;
            var customValue = specificationAttributeManager.GetProperty("CustomValue").StringValue;
            var displayOrder = specificationAttributeManager.GetProperty("DisplayOrder").IntValue;
            var showOnProductPage = specificationAttributeManager.GetProperty("ShowOnProductPage").BooleanValue;

            //if specification attribute option isn't set, try to get first of possible specification attribute option for current specification attribute
            if (specificationAttributeOptionId == 0)
            {
                var specificationAttribute = specificationAttributeManager.GetProperty("SpecificationAttribute").IntValue;
                specificationAttributeOptionId =
                    (await _specificationAttributeService.GetSpecificationAttributeOptionsBySpecificationAttributeAsync(
                        specificationAttribute))
                    .FirstOrDefault()?.Id ?? specificationAttributeOptionId;
            }

            var productSpecificationAttribute = specificationAttributeOptionId == 0
                ? null
                : (await _specificationAttributeService.GetProductSpecificationAttributesAsync(productId, specificationAttributeOptionId)).FirstOrDefault();

            var isNew = productSpecificationAttribute == null;

            if (isNew) productSpecificationAttribute = new ProductSpecificationAttribute();

            if (attributeTypeId != (int)SpecificationAttributeType.Option)
                //we allow filtering only for "Option" attribute type
                allowFiltering = false;

            //we don't allow CustomValue for "Option" attribute type
            if (attributeTypeId == (int)SpecificationAttributeType.Option) 
                customValue = null;

            productSpecificationAttribute.AttributeTypeId = attributeTypeId;
            productSpecificationAttribute.SpecificationAttributeOptionId = specificationAttributeOptionId;
            productSpecificationAttribute.ProductId = productId;
            productSpecificationAttribute.CustomValue = customValue;
            productSpecificationAttribute.AllowFiltering = allowFiltering;
            productSpecificationAttribute.ShowOnProductPage = showOnProductPage;
            productSpecificationAttribute.DisplayOrder = displayOrder;

            if (isNew)
                await _specificationAttributeService.InsertProductSpecificationAttributeAsync(productSpecificationAttribute);
            else
                await _specificationAttributeService.UpdateProductSpecificationAttributeAsync(productSpecificationAttribute);
        }

        /// <returns>A task that represents the asynchronous operation</returns>
        private async Task<string> DownloadFileAsync(string urlString, IList<string> downloadedFiles)
        {
            if (string.IsNullOrEmpty(urlString))
                return string.Empty;

            if (!Uri.IsWellFormedUriString(urlString, UriKind.Absolute))
                return urlString;

            if (!_catalogSettings.ExportImportAllowDownloadImages)
                return string.Empty;

            //ensure that temp directory is created
            var tempDirectory = _fileProvider.MapPath(ExportImportDefaults.UploadsTempPath);
            _fileProvider.CreateDirectory(tempDirectory);

            var fileName = _fileProvider.GetFileName(urlString);
            if (string.IsNullOrEmpty(fileName))
                return string.Empty;

            var filePath = _fileProvider.Combine(tempDirectory, fileName);
            try
            {
                var client = _httpClientFactory.CreateClient(NopHttpDefaults.DefaultHttpClient);
                var fileData = await client.GetByteArrayAsync(urlString);
                await using (var fs = new FileStream(filePath, FileMode.OpenOrCreate)) 
                    fs.Write(fileData, 0, fileData.Length);

                downloadedFiles?.Add(filePath);
                return filePath;
            }
            catch (Exception ex)
            {
                await _logger.ErrorAsync("Download image failed", ex);
            }

            return string.Empty;
        }

        /// <returns>A task that represents the asynchronous operation</returns>
        private async Task<ImportProductMetadata> PrepareImportProductDataAsync(IXLWorksheet worksheet)
        {
            //the columns
            var properties = GetPropertiesByExcelCells<Product>(worksheet);

            var manager = new PropertyManager<Product>(properties, _catalogSettings);

            var productAttributeProperties = new[]
            {
                new PropertyByName<ExportProductAttribute>("AttributeId"),
                new PropertyByName<ExportProductAttribute>("AttributeName"),
                new PropertyByName<ExportProductAttribute>("DefaultValue"),
                new PropertyByName<ExportProductAttribute>("ValidationMinLength"),
                new PropertyByName<ExportProductAttribute>("ValidationMaxLength"),
                new PropertyByName<ExportProductAttribute>("ValidationFileAllowedExtensions"),
                new PropertyByName<ExportProductAttribute>("ValidationFileMaximumSize"),
                new PropertyByName<ExportProductAttribute>("AttributeTextPrompt"),
                new PropertyByName<ExportProductAttribute>("AttributeIsRequired"),
                new PropertyByName<ExportProductAttribute>("AttributeControlType"),
                new PropertyByName<ExportProductAttribute>("AttributeDisplayOrder"),
                new PropertyByName<ExportProductAttribute>("ProductAttributeValueId"),
                new PropertyByName<ExportProductAttribute>("ValueName"),
                new PropertyByName<ExportProductAttribute>("AttributeValueType"),
                new PropertyByName<ExportProductAttribute>("AssociatedProductId"),
                new PropertyByName<ExportProductAttribute>("ColorSquaresRgb"),
                new PropertyByName<ExportProductAttribute>("ImageSquaresPictureId"),
                new PropertyByName<ExportProductAttribute>("PriceAdjustment"),
                new PropertyByName<ExportProductAttribute>("PriceAdjustmentUsePercentage"),
                new PropertyByName<ExportProductAttribute>("WeightAdjustment"),
                new PropertyByName<ExportProductAttribute>("Cost"),
                new PropertyByName<ExportProductAttribute>("CustomerEntersQty"),
                new PropertyByName<ExportProductAttribute>("Quantity"),
                new PropertyByName<ExportProductAttribute>("IsPreSelected"),
                new PropertyByName<ExportProductAttribute>("DisplayOrder"),
                new PropertyByName<ExportProductAttribute>("PictureId")
            };

            var productAttributeManager = new PropertyManager<ExportProductAttribute>(productAttributeProperties, _catalogSettings);

            var specificationAttributeProperties = new[]
            {
                new PropertyByName<ExportSpecificationAttribute>("AttributeType", p => p.AttributeTypeId),
                new PropertyByName<ExportSpecificationAttribute>("SpecificationAttribute", p => p.SpecificationAttributeId),
                new PropertyByName<ExportSpecificationAttribute>("CustomValue", p => p.CustomValue),
                new PropertyByName<ExportSpecificationAttribute>("SpecificationAttributeOptionId", p => p.SpecificationAttributeOptionId),
                new PropertyByName<ExportSpecificationAttribute>("AllowFiltering", p => p.AllowFiltering),
                new PropertyByName<ExportSpecificationAttribute>("ShowOnProductPage", p => p.ShowOnProductPage),
                new PropertyByName<ExportSpecificationAttribute>("DisplayOrder", p => p.DisplayOrder)
            };

            var specificationAttributeManager = new PropertyManager<ExportSpecificationAttribute>(specificationAttributeProperties, _catalogSettings);

            var endRow = 2;
            var allCategories = new List<string>();
            var allSku = new List<string>();

            var tempProperty = manager.GetProperty("Categories");
            var categoryCellNum = tempProperty?.PropertyOrderPosition ?? -1;

            tempProperty = manager.GetProperty("SKU");
            var skuCellNum = tempProperty?.PropertyOrderPosition ?? -1;

            var allManufacturers = new List<string>();
            tempProperty = manager.GetProperty("Manufacturers");
            var manufacturerCellNum = tempProperty?.PropertyOrderPosition ?? -1;

            var allStores = new List<string>();
            tempProperty = manager.GetProperty("LimitedToStores");
            var limitedToStoresCellNum = tempProperty?.PropertyOrderPosition ?? -1;

            if (_catalogSettings.ExportImportUseDropdownlistsForAssociatedEntities)
            {
                productAttributeManager.SetSelectList("AttributeControlType", await AttributeControlType.TextBox.ToSelectListAsync(useLocalization: false));
                productAttributeManager.SetSelectList("AttributeValueType", await AttributeValueType.Simple.ToSelectListAsync(useLocalization: false));

                specificationAttributeManager.SetSelectList("AttributeType", await SpecificationAttributeType.Option.ToSelectListAsync(useLocalization: false));
                specificationAttributeManager.SetSelectList("SpecificationAttribute", (await _specificationAttributeService
                    .GetSpecificationAttributesAsync())
                    .Select(sa => sa as BaseEntity)
                    .ToSelectList(p => (p as SpecificationAttribute)?.Name ?? string.Empty));

                manager.SetSelectList("ProductType", await ProductType.SimpleProduct.ToSelectListAsync(useLocalization: false));
                manager.SetSelectList("GiftCardType", await GiftCardType.Virtual.ToSelectListAsync(useLocalization: false));
                manager.SetSelectList("DownloadActivationType",
                    await DownloadActivationType.Manually.ToSelectListAsync(useLocalization: false));
                manager.SetSelectList("ManageInventoryMethod",
                    await ManageInventoryMethod.DontManageStock.ToSelectListAsync(useLocalization: false));
                manager.SetSelectList("LowStockActivity",
                    await LowStockActivity.Nothing.ToSelectListAsync(useLocalization: false));
                manager.SetSelectList("BackorderMode", await BackorderMode.NoBackorders.ToSelectListAsync(useLocalization: false));
                manager.SetSelectList("RecurringCyclePeriod",
                    await RecurringProductCyclePeriod.Days.ToSelectListAsync(useLocalization: false));
                manager.SetSelectList("RentalPricePeriod", await RentalPricePeriod.Days.ToSelectListAsync(useLocalization: false));

                manager.SetSelectList("Vendor",
                    (await _vendorService.GetAllVendorsAsync(showHidden: true)).Select(v => v as BaseEntity)
                        .ToSelectList(p => (p as Vendor)?.Name ?? string.Empty));
                manager.SetSelectList("ProductTemplate",
                    (await _productTemplateService.GetAllProductTemplatesAsync()).Select(pt => pt as BaseEntity)
                        .ToSelectList(p => (p as ProductTemplate)?.Name ?? string.Empty));
                manager.SetSelectList("DeliveryDate",
                    (await _dateRangeService.GetAllDeliveryDatesAsync()).Select(dd => dd as BaseEntity)
                        .ToSelectList(p => (p as DeliveryDate)?.Name ?? string.Empty));
                manager.SetSelectList("ProductAvailabilityRange",
                    (await _dateRangeService.GetAllProductAvailabilityRangesAsync()).Select(range => range as BaseEntity)
                        .ToSelectList(p => (p as ProductAvailabilityRange)?.Name ?? string.Empty));
                manager.SetSelectList("TaxCategory",
                    (await _taxCategoryService.GetAllTaxCategoriesAsync()).Select(tc => tc as BaseEntity)
                        .ToSelectList(p => (p as TaxCategory)?.Name ?? string.Empty));
                manager.SetSelectList("BasepriceUnit",
                    (await _measureService.GetAllMeasureWeightsAsync()).Select(mw => mw as BaseEntity)
                        .ToSelectList(p => (p as MeasureWeight)?.Name ?? string.Empty));
                manager.SetSelectList("BasepriceBaseUnit",
                    (await _measureService.GetAllMeasureWeightsAsync()).Select(mw => mw as BaseEntity)
                        .ToSelectList(p => (p as MeasureWeight)?.Name ?? string.Empty));
            }

            var allAttributeIds = new List<int>();
            var allSpecificationAttributeOptionIds = new List<int>();

            var attributeIdCellNum = 1 + ExportProductAttribute.ProducAttributeCellOffset;
            var specificationAttributeOptionIdCellNum =
                specificationAttributeManager.GetIndex("SpecificationAttributeOptionId") +
                ExportProductAttribute.ProducAttributeCellOffset;

            var productsInFile = new List<int>();

            //find end of data
            var typeOfExportedAttribute = ExportedAttributeType.NotSpecified;
            while (true)
            {
                var allColumnsAreEmpty = manager.GetProperties
                    .Select(property => worksheet.Row(endRow).Cell(property.PropertyOrderPosition))
                    .All(cell => string.IsNullOrEmpty(cell?.Value?.ToString()));

                if (allColumnsAreEmpty)
                    break;

                if (new[] { 1, 2 }.Select(cellNum => worksheet.Row(endRow).Cell(cellNum))
                        .All(cell => string.IsNullOrEmpty(cell?.Value?.ToString())) &&
                    worksheet.Row(endRow).OutlineLevel == 0)
                {
                    var cellValue = worksheet.Row(endRow).Cell(attributeIdCellNum).Value;
                    await SetOutLineForProductAttributeRowAsync(cellValue, worksheet, endRow);
                    await SetOutLineForSpecificationAttributeRowAsync(cellValue, worksheet, endRow);
                }

                if (worksheet.Row(endRow).OutlineLevel != 0)
                {
                    var newTypeOfExportedAttribute = GetTypeOfExportedAttribute(worksheet, productAttributeManager, specificationAttributeManager, endRow);

                    //skip caption row
                    if (newTypeOfExportedAttribute != ExportedAttributeType.NotSpecified && newTypeOfExportedAttribute != typeOfExportedAttribute)
                    {
                        typeOfExportedAttribute = newTypeOfExportedAttribute;
                        endRow++;
                        continue;
                    }

                    switch (typeOfExportedAttribute)
                    {
                        case ExportedAttributeType.ProductAttribute:
                            productAttributeManager.ReadFromXlsx(worksheet, endRow,
                                ExportProductAttribute.ProducAttributeCellOffset);
                            if (int.TryParse((worksheet.Row(endRow).Cell(attributeIdCellNum).Value ?? string.Empty).ToString(), out var aid))
                            {
                                allAttributeIds.Add(aid);
                            }

                            break;
                        case ExportedAttributeType.SpecificationAttribute:
                            specificationAttributeManager.ReadFromXlsx(worksheet, endRow, ExportProductAttribute.ProducAttributeCellOffset);

                            if (int.TryParse((worksheet.Row(endRow).Cell(specificationAttributeOptionIdCellNum).Value ?? string.Empty).ToString(), out var saoid))
                            {
                                allSpecificationAttributeOptionIds.Add(saoid);
                            }

                            break;
                    }

                    endRow++;
                    continue;
                }

                if (categoryCellNum > 0)
                {
                    var categoryIds = worksheet.Row(endRow).Cell(categoryCellNum).Value?.ToString() ?? string.Empty;

                    if (!string.IsNullOrEmpty(categoryIds))
                        allCategories.AddRange(categoryIds
                            .Split(new[] { ";", ">>" }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim())
                            .Distinct());
                }

                if (skuCellNum > 0)
                {
                    var sku = worksheet.Row(endRow).Cell(skuCellNum).Value?.ToString() ?? string.Empty;

                    if (!string.IsNullOrEmpty(sku))
                        allSku.Add(sku);
                }

                if (manufacturerCellNum > 0)
                {
                    var manufacturerIds = worksheet.Row(endRow).Cell(manufacturerCellNum).Value?.ToString() ??
                                          string.Empty;
                    if (!string.IsNullOrEmpty(manufacturerIds))
                        allManufacturers.AddRange(manufacturerIds
                            .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()));
                }

                if (limitedToStoresCellNum > 0)
                {
                    var storeIds = worksheet.Row(endRow).Cell(limitedToStoresCellNum).Value?.ToString() ??
                                          string.Empty;
                    if (!string.IsNullOrEmpty(storeIds))
                        allStores.AddRange(storeIds
                            .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()));
                }

                //counting the number of products
                productsInFile.Add(endRow);

                endRow++;
            }

            //performance optimization, the check for the existence of the categories in one SQL request
            var notExistingCategories = await _categoryService.GetNotExistingCategoriesAsync(allCategories.ToArray());
            if (notExistingCategories.Any())
            {
                throw new ArgumentException(string.Format(await _localizationService.GetResourceAsync("Admin.Catalog.Products.Import.CategoriesDontExist"), string.Join(", ", notExistingCategories)));
            }

            //performance optimization, the check for the existence of the manufacturers in one SQL request
            var notExistingManufacturers = await _manufacturerService.GetNotExistingManufacturersAsync(allManufacturers.ToArray());
            if (notExistingManufacturers.Any())
            {
                throw new ArgumentException(string.Format(await _localizationService.GetResourceAsync("Admin.Catalog.Products.Import.ManufacturersDontExist"), string.Join(", ", notExistingManufacturers)));
            }

            //performance optimization, the check for the existence of the product attributes in one SQL request
            var notExistingProductAttributes = await _productAttributeService.GetNotExistingAttributesAsync(allAttributeIds.ToArray());
            if (notExistingProductAttributes.Any())
            {
                throw new ArgumentException(string.Format(await _localizationService.GetResourceAsync("Admin.Catalog.Products.Import.ProductAttributesDontExist"), string.Join(", ", notExistingProductAttributes)));
            }

            //performance optimization, the check for the existence of the specification attribute options in one SQL request
            var notExistingSpecificationAttributeOptions = await _specificationAttributeService.GetNotExistingSpecificationAttributeOptionsAsync(allSpecificationAttributeOptionIds.Where(saoId => saoId != 0).ToArray());
            if (notExistingSpecificationAttributeOptions.Any())
            {
                throw new ArgumentException($"The following specification attribute option ID(s) don't exist - {string.Join(", ", notExistingSpecificationAttributeOptions)}");
            }

            //performance optimization, the check for the existence of the stores in one SQL request
            var notExistingStores = await _storeService.GetNotExistingStoresAsync(allStores.ToArray());
            if (notExistingStores.Any())
            {
                throw new ArgumentException(string.Format(await _localizationService.GetResourceAsync("Admin.Catalog.Products.Import.StoresDontExist"), string.Join(", ", notExistingStores)));
            }

            return new ImportProductMetadata
            {
                EndRow = endRow,
                Manager = manager,
                Properties = properties,
                ProductsInFile = productsInFile,
                ProductAttributeManager = productAttributeManager,
                SpecificationAttributeManager = specificationAttributeManager,
                SkuCellNum = skuCellNum,
                AllSku = allSku
            };
        }

        /// <returns>A task that represents the asynchronous operation</returns>
        private async Task ImportProductsFromSplitedXlsxAsync(IXLWorksheet worksheet, ImportProductMetadata metadata)
        {
            foreach (var path in SplitProductFile(worksheet, metadata))
            {
                using var scope = _serviceScopeFactory.CreateScope();
                // Resolve
                var importManager = EngineContext.Current.Resolve<IImportManager>(scope);

                using var sr = new StreamReader(path);
                await importManager.ImportProductsFromXlsxAsync(sr.BaseStream);

                try
                {
                    _fileProvider.DeleteFile(path);
                }
                catch
                {
                    // ignored
                }
            }
        }

        private IList<string> SplitProductFile(IXLWorksheet worksheet, ImportProductMetadata metadata)
        {
            var fileIndex = 1;
            var fileName = Guid.NewGuid().ToString();
            var endCell = metadata.Properties.Max(p => p.PropertyOrderPosition);

            var filePaths = new List<string>();

            while (true)
            {
                var curIndex = fileIndex * _catalogSettings.ExportImportProductsCountInOneFile;

                var startRow = metadata.ProductsInFile[(fileIndex - 1) * _catalogSettings.ExportImportProductsCountInOneFile];

                var endRow = metadata.CountProductsInFile > curIndex + 1
                    ? metadata.ProductsInFile[curIndex - 1]
                    : metadata.EndRow;

                var filePath = $"{_fileProvider.MapPath(ExportImportDefaults.UploadsTempPath)}/{fileName}_part_{fileIndex}.xlsx";

                CopyDataToNewFile(metadata, worksheet, filePath, startRow, endRow, endCell);

                filePaths.Add(filePath);
                fileIndex += 1;

                if (endRow == metadata.EndRow)
                    break;
            }

            return filePaths;
        }

        private ImportRolesColumnMetadata PrepareImportRolesForTierPricingDataAsync(IXLWorksheet worksheet)
        {
            var properties = GetPropertiesByExcelCells<TierPriceRoleImport>(worksheet);
            var manager = new PropertyManager<TierPriceRoleImport>(properties, _catalogSettings);
            var row = 2;
            
            List<RoleDetail> RoleDetailList = new List<RoleDetail>();

            while (true)
            {
                var allColumnsAreEmpty = manager.GetProperties
                    .Select(property => worksheet.Row(row).Cell(property.PropertyOrderPosition))
                    .All(cell => string.IsNullOrEmpty(cell?.Value?.ToString()));

                if (allColumnsAreEmpty)
                    break;

                manager.ReadFromXlsx(worksheet, row);

                var Role = manager.GetProperty("Role Name").StringValue;
                var Column = manager.GetProperty("Tier Column").StringValue;

                RoleDetailList.Add(new RoleDetail(Role, Column));

                row++;
            }

            return new ImportRolesColumnMetadata {
                Manager = manager,
                Properties = properties,
                RoleDetails = RoleDetailList
            };;
        }

        private async Task<ImportTierPriceMetadata> PrepareImportPricesForTierPricingDataAsync(IList<RoleDetail> RoleDetailList, int StoreId, IXLWorksheet worksheet)
        {
            var properties = GetPropertiesByExcelCells<TierPriceImport>(worksheet);
            var manager = new PropertyManager<TierPriceImport>(properties, _catalogSettings);

            var row = 2;

            List<string> AllSku = new List<string>();
            List<TierPriceImport> AllImportObject = new List<TierPriceImport>();

            while (true)
            {
                var allColumnsAreEmpty = manager.GetProperties
                    .Select(property => worksheet.Row(row).Cell(property.PropertyOrderPosition))
                    .All(cell => string.IsNullOrEmpty(cell?.Value?.ToString()));

                if (allColumnsAreEmpty)
                    break;


                manager.ReadFromXlsx(worksheet, row);
                
                TierPriceImport ImportObject = new TierPriceImport();

                foreach (var property in manager.GetProperties)
                {
                    var value = property.PropertyValue;
                    switch (property.PropertyName)
                    {
                        case "Item Name":
                            ImportObject.Name = property.StringValue;
                            break;
                        case "Item Number":
                            ImportObject.SKU = property.StringValue;
                            AllSku.Add(property.StringValue);
                            break;
                        case "Web Store Price":
                            ImportObject.ProductPrice = (decimal)property.DoubleValue;
                            break;
                        case "Price Level A, Qty Break 1":
                            ImportObject.PriceForTireOne = property.DoubleValue;
                            ImportObject.RoleForTireOne = RoleDetailList.FirstOrDefault(i => i.Column == property.PropertyName).Role;
                            break;
                        case "Price Level B, Qty Break 1":
                            ImportObject.PriceForTireTwo = property.DoubleValue;
                            ImportObject.RoleForTireTwo = RoleDetailList.FirstOrDefault(i => i.Column == property.PropertyName).Role;
                            break;
                        case "Price Level C, Qty Break 1":
                            ImportObject.PriceForTireThree = property.DoubleValue;
                            ImportObject.RoleForTireThree = RoleDetailList.FirstOrDefault(i => i.Column == property.PropertyName).Role;
                            break;
                        case "Price Level D, Qty Break 1":
                            ImportObject.PriceForTireFour = property.DoubleValue;
                            ImportObject.RoleForTireFour = RoleDetailList.FirstOrDefault(i => i.Column == property.PropertyName).Role;
                            break;
                        case "Price Level E, Qty Break 1":
                            ImportObject.PriceForTireFive = property.DoubleValue;
                            ImportObject.RoleForTireFive = RoleDetailList.FirstOrDefault(i => i.Column == property.PropertyName).Role;
                            break;
                        case "Price Level F, Qty Break 1":
                            ImportObject.PriceForTireSix = property.DoubleValue;
                            ImportObject.RoleForTireSix = RoleDetailList.FirstOrDefault(i => i.Column == property.PropertyName).Role;
                            break;
                    }
                }
                
                AllImportObject.Add(ImportObject);
                row++;
            }

            var allProductsBySku = await _productService.GetProductsBySkuAsync(AllSku.ToArray(), (await _workContext.GetCurrentVendorAsync())?.Id ?? 0);

            return new ImportTierPriceMetadata {
                Properties = properties,
                Manager = manager,
                ProductList = allProductsBySku,
                ImportTierPriceList = AllImportObject,
                ProductsInFile = allProductsBySku.Count
            };
        }
        
        #endregion

        #region Methods

        /// <summary>
        /// Get property list by excel cells
        /// </summary>
        /// <typeparam name="T">Type of object</typeparam>
        /// <param name="worksheet">Excel worksheet</param>
        /// <returns>Property list</returns>
        public static IList<PropertyByName<T>> GetPropertiesByExcelCells<T>(IXLWorksheet worksheet)
        {
            var properties = new List<PropertyByName<T>>();
            var poz = 1;
            while (true)
            {
                try
                {
                    var cell = worksheet.Row(1).Cell(poz);

                    if (string.IsNullOrEmpty(cell?.Value?.ToString()))
                        break;

                    poz += 1;
                    properties.Add(new PropertyByName<T>(cell.Value.ToString()));
                }
                catch
                {
                    break;
                }
            }

            return properties;
        }

        /// <summary>
        /// Foramt product XLSX file for Importing process 
        /// </summary>
        /// <param name="stream">Stream</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        public virtual async Task FormatProductXlsxToImport(Stream stream)
        {
            using var workbook = new XLWorkbook(stream);
            // get the first worksheet in the workbook
            var worksheet = workbook.Worksheets.FirstOrDefault();
            if (worksheet == null)
                throw new NopException("No worksheet found");

            var properties = GetPropertiesByExcelCells<Product>(worksheet);

            if (properties.SingleOrDefault(i => i.PropertyName == "Title en") == null)
            {
                await ImportProductsFromXlsxAsync(stream);
                return;
            }

            var manager = new PropertyManager<Product>(properties, _catalogSettings);
            var URL = "https://www.premacanada.ca";

            var AllCategories = await _categoryService.GetAllCategoriesAsync();

            List<ProductImportModel> ProductsInXlsx = new List<ProductImportModel>();

            var customer = await _workContext.GetCurrentCustomerAsync();
            var IsLimitedToStores = !await _customerService.IsAdminAsync(customer);

            var iRow = 2;

            while (true)
            {
                var allColumnsAreEmpty = manager.GetProperties
                    .Select(property => worksheet.Row(iRow).Cell(property.PropertyOrderPosition))
                    .All(cell => cell?.Value == null || string.IsNullOrEmpty(cell.Value.ToString()));

                if (allColumnsAreEmpty)
                    break;

                manager.ReadFromXlsx(worksheet, iRow);

                var product = new ProductImportModel();

                foreach (var property in manager.GetProperties)
                {
                    var category = new Category();
                    switch (property.PropertyName)
                    {
                        case "Title en":
                            product.Name = property.StringValue;
                            break;
                        case "Description Small en":
                            product.ShortDescription = property.StringValue;
                            break;
                        case "Description Default en":
                            product.FullDescription = property.StringValue;
                            break;
                        case "Status":
                            product.Published = property.StringValue == "Enable";
                            break;
                        case "Code":
                            product.SKU = property.StringValue;
                            break;
                        case "Quantity minimum":
                            product.OrderMinimumQuantity = property.IntValue;
                            break;
                        case "Weight Kg":
                            product.Weight = property.DecimalValue;
                            break;
                        case "Length Cm":
                            product.Length = property.DecimalValue;
                            break;
                        case "Width Cm":
                            product.Width = property.DecimalValue;
                            break;
                        case "Height Cm":
                            product.Height = property.DecimalValue;
                            break;
                        case "Brand":
                            product.Manufacturers = property.StringValue + ";";
                            break;
                        case "Description MetaDescription en":
                            if (property.StringValue == null || property.StringValue == "") break;
                            product.MetaDescription = property.StringValue + ";";
                            break;
                        case "Description MetaKeywords en":
                            if (property.StringValue == null || property.StringValue == "") break;
                            product.MetaKeywords = property.StringValue + ";";
                            break;
                        case "Description PageTitle en":
                            if (property.StringValue == null || property.StringValue == "") break;
                            product.MetaTitle = property.StringValue + ";";
                            break;
                        case "Category 1":
                            if (property.StringValue != "")
                            {
                                category = AllCategories.SingleOrDefault(i => i.CodeId == property.StringValue.Trim());
                                if (category != null) product.Categories += category.Id + ";";
                            }
                            break;
                        case "Category 2":
                            if (property.StringValue != "")
                            {
                                category = AllCategories.SingleOrDefault(i => i.CodeId == property.StringValue.Trim());
                                if (category != null) product.Categories += category.Id + ";";
                            }
                            break;
                        case "Category 3":
                            if (property.StringValue != "")
                            {
                                category = AllCategories.SingleOrDefault(i => i.CodeId == property.StringValue.Trim());
                                if (category != null) product.Categories += category.Id + ";";
                            }
                            break;
                        case "Category 4":
                            if (property.StringValue != "")
                            {
                                category = AllCategories.SingleOrDefault(i => i.CodeId == property.StringValue.Trim());
                                if (category != null) product.Categories += category.Id + ";";
                            }
                            break;
                        case "Picture 1 Large":
                            if (property.StringValue == null || property.StringValue == "") break;
                            product.Picture1 = URL + property.StringValue;
                            break;
                        case "Picture 2 Large":
                            if (property.StringValue == null || property.StringValue == "") break;
                            product.Picture2 = URL + property.StringValue;
                            break;
                        case "Picture 3 Large":
                            if (property.StringValue == null || property.StringValue == "") break;
                            product.Picture3 = URL + property.StringValue;
                            break;
                        case "Complementary 1":
                            if (property.StringValue == null || property.StringValue == "") break;
                            product.RelatedProducts += property.StringValue + ";";
                            break;
                        case "Complementary 2":
                            if (property.StringValue == null || property.StringValue == "") break;
                            product.RelatedProducts += property.StringValue + ";";
                            break;
                        case "Complementary 3":
                            if (property.StringValue == null || property.StringValue == "") break;
                            product.RelatedProducts += property.StringValue + ";";
                            break;
                        case "Complementary 4":
                            if (property.StringValue == null || property.StringValue == "") break;
                            product.RelatedProducts += property.StringValue + ";";
                            break;
                        case "Complementary 5":
                            if (property.StringValue == null || property.StringValue == "") break;
                            product.RelatedProducts += property.StringValue + ";";
                            break;
                        case "Complementary 6":
                            if (property.StringValue == null || property.StringValue == "") break;
                            product.RelatedProducts += property.StringValue + ";";
                            break;
                        case "Complementary 7":
                            if (property.StringValue == null || property.StringValue == "") break;
                            product.RelatedProducts += property.StringValue + ";";
                            break;
                        case "Complementary 8":
                            if (property.StringValue == null || property.StringValue == "") break;
                            product.RelatedProducts += property.StringValue + ";";
                            break;
                        case "Complementary 9":
                            if (property.StringValue == null || property.StringValue == "") break;
                            product.RelatedProducts += property.StringValue + ";";
                            break;
                        case "Complementary 10":
                            if (property.StringValue == null || property.StringValue == "") break;
                            product.RelatedProducts += property.StringValue + ";";
                            break;
                        case "Complementary 11":
                            if (property.StringValue == null || property.StringValue == "") break;
                            product.RelatedProducts += property.StringValue + ";";
                            break;
                        case "Complementary 12":
                            if (property.StringValue == null || property.StringValue == "") break;
                            product.RelatedProducts += property.StringValue + ";";
                            break;
                        case "Complementary 13":
                            if (property.StringValue == null || property.StringValue == "") break;
                            product.RelatedProducts += property.StringValue + ";";
                            break;
                    }
                }

                if (true)
                {
                    product.ProductType = 5;
                    product.ParentGroupedProductId = 0;
                    product.VisibleIndividually = true;
                    product.Vendor = 0;
                    product.ProductTemplate = 0;
                    product.ShowOnHomepage = false;
                    product.DisplayOrder = 0;
                    product.AllowCustomerReviews = false;
                    product.ManufacturerPartNumber = "";
                    product.Gtin = "";
                    product.IsGiftCard = false;
                    product.GiftCardType = 0;
                    product.OverriddenGiftCardAmount = "";
                    product.RequireOtherProducts = false;
                    product.RequiredProductIds = "";
                    product.AutomaticallyAddRequiredProducts = false;
                    product.IsDownload = false;
                    product.DownloadId = 0;
                    product.UnlimitedDownloads = false;
                    product.MaxNumberOfDownloads = 0;
                    product.DownloadActivationType = 0;
                    product.HasSampleDownload = false;
                    product.SampleDownloadId = 0;
                    product.HasUserAgreement = false;
                    product.UserAgreementText = "";
                    product.IsRecurring = false;
                    product.RecurringCycleLength = 0;
                    product.RecurringCyclePeriod = 0;
                    product.RecurringTotalCycles = 0;
                    product.IsRental = false;
                    product.RentalPriceLength = 0;
                    product.RentalPricePeriod = 0;
                    product.IsShipEnabled = true;
                    product.IsFreeShipping = false;
                    product.ShipSeparately = false;
                    product.AdditionalShippingCharge = 0;
                    product.DeliveryDate = 0;
                    product.IsTaxExempt = false;
                    product.TaxCategory = 0;
                    product.IsTelecommunicationsOrBroadcastingOrElectronicServices = false;
                    product.ManageInventoryMethod = 1;
                    product.ProductAvailabilityRange = 0;
                    product.UseMultipleWarehouses = false;
                    product.WarehouseId = 0;
                    product.StockQuantity = 100;
                    product.DisplayStockAvailability = false;
                    product.DisplayStockQuantity = false;
                    product.MinStockQuantity = 0;
                    product.LowStockActivity = 0;
                    product.NotifyAdminForQuantityBelow = 0;
                    product.BackorderMode = 0;
                    product.AllowBackInStockSubscriptions = false;
                    product.OrderMaximumQuantity = 1000;
                    product.AllowedQuantities = "";
                    product.AllowAddingOnlyExistingAttributeCombinations = false;
                    product.NotReturnable = false;
                    product.DisableBuyButton = false;
                    product.DisableWishlistButton = false;
                    product.AvailableForPreOrder = false;
                    product.PreOrderAvailabilityStartDateTimeUtc = "";
                    product.CallForPrice = false;
                    product.Price = 0;
                    product.OldPrice = 0;
                    product.ProductCost = 0;
                    product.CustomerEntersPrice = false;
                    product.MinimumCustomerEnteredPrice = 0;
                    product.MaximumCustomerEnteredPrice = 0;
                    product.BasepriceEnabled = false;
                    product.BasepriceAmount = 0;
                    product.BasepriceUnit = 0;
                    product.BasepriceBaseAmount = 0;
                    product.BasepriceBaseUnit = 0;
                    product.MarkAsNew = false;
                    product.MarkAsNewStartDateTimeUtc = "";
                    product.MarkAsNewEndDateTimeUtc = "";
                    product.ProductTags = "";
                    product.IsLimitedToStores = IsLimitedToStores;
                }
                ProductsInXlsx.Add(product);
                iRow++;
            }
            var ExportProperties = new[]
            {
                new PropertyByName<ProductImportModel>("ProductType", p => p.ProductType),
                new PropertyByName<ProductImportModel>("ParentGroupedProductId", p => p.ParentGroupedProductId),
                new PropertyByName<ProductImportModel>("VisibleIndividually", p => p.VisibleIndividually),
                new PropertyByName<ProductImportModel>("Name", p => p.Name),
                new PropertyByName<ProductImportModel>("ShortDescription", p => p.ShortDescription),
                new PropertyByName<ProductImportModel>("FullDescription", p => p.FullDescription),
                new PropertyByName<ProductImportModel>("Vendor", p => p.Vendor),
                new PropertyByName<ProductImportModel>("ProductTemplate", p => p.ProductTemplate),
                new PropertyByName<ProductImportModel>("ShowOnHomepage", p => p.ShowOnHomepage),
                new PropertyByName<ProductImportModel>("DisplayOrder", p => p.DisplayOrder),
                new PropertyByName<ProductImportModel>("MetaKeywords", p => p.MetaKeywords),
                new PropertyByName<ProductImportModel>("MetaDescription", p => p.MetaDescription),
                new PropertyByName<ProductImportModel>("MetaTitle", p => p.MetaTitle),
                new PropertyByName<ProductImportModel>("SeName", p => p.SeName),
                new PropertyByName<ProductImportModel>("AllowCustomerReviews", p => p.AllowCustomerReviews),
                new PropertyByName<ProductImportModel>("Published", p => p.Published),
                new PropertyByName<ProductImportModel>("SKU", p => p.SKU),
                new PropertyByName<ProductImportModel>("ManufacturerPartNumber", p => p.ManufacturerPartNumber),
                new PropertyByName<ProductImportModel>("Gtin", p => p.Gtin),
                new PropertyByName<ProductImportModel>("IsGiftCard", p => p.IsGiftCard),
                new PropertyByName<ProductImportModel>("GiftCardType", p => p.GiftCardType),
                new PropertyByName<ProductImportModel>("OverriddenGiftCardAmount", p => p.OverriddenGiftCardAmount),
                new PropertyByName<ProductImportModel>("RequireOtherProducts", p => p.RequireOtherProducts),
                new PropertyByName<ProductImportModel>("RequiredProductIds", p => p.RequiredProductIds),
                new PropertyByName<ProductImportModel>("AutomaticallyAddRequiredProducts", p => p.AutomaticallyAddRequiredProducts),
                new PropertyByName<ProductImportModel>("IsDownload", p => p.IsDownload),
                new PropertyByName<ProductImportModel>("DownloadId", p => p.DownloadId),
                new PropertyByName<ProductImportModel>("UnlimitedDownloads", p => p.UnlimitedDownloads),
                new PropertyByName<ProductImportModel>("MaxNumberOfDownloads", p => p.MaxNumberOfDownloads),
                new PropertyByName<ProductImportModel>("DownloadActivationType", p => p.DownloadActivationType),
                new PropertyByName<ProductImportModel>("HasSampleDownload", p => p.HasSampleDownload),
                new PropertyByName<ProductImportModel>("SampleDownloadId", p => p.SampleDownloadId),
                new PropertyByName<ProductImportModel>("HasUserAgreement", p => p.HasUserAgreement),
                new PropertyByName<ProductImportModel>("UserAgreementText", p => p.UserAgreementText),
                new PropertyByName<ProductImportModel>("IsRecurring", p => p.IsRecurring),
                new PropertyByName<ProductImportModel>("RecurringCycleLength", p => p.RecurringCycleLength),
                new PropertyByName<ProductImportModel>("RecurringCyclePeriod", p => p.RecurringCyclePeriod),
                new PropertyByName<ProductImportModel>("RecurringTotalCycles", p => p.RecurringTotalCycles),
                new PropertyByName<ProductImportModel>("IsRental", p => p.IsRental),
                new PropertyByName<ProductImportModel>("RentalPriceLength", p => p.RentalPriceLength),
                new PropertyByName<ProductImportModel>("RentalPricePeriod", p => p.RentalPricePeriod),
                new PropertyByName<ProductImportModel>("IsShipEnabled", p => p.IsShipEnabled),
                new PropertyByName<ProductImportModel>("IsFreeShipping", p => p.IsFreeShipping),
                new PropertyByName<ProductImportModel>("ShipSeparately", p => p.ShipSeparately),
                new PropertyByName<ProductImportModel>("AdditionalShippingCharge", p => p.AdditionalShippingCharge),
                new PropertyByName<ProductImportModel>("DeliveryDate", p => p.DeliveryDate),
                new PropertyByName<ProductImportModel>("IsTaxExempt", p => p.IsTaxExempt),
                new PropertyByName<ProductImportModel>("TaxCategory", p => p.TaxCategory),
                new PropertyByName<ProductImportModel>("IsTelecommunicationsOrBroadcastingOrElectronicServices", p => p.IsTelecommunicationsOrBroadcastingOrElectronicServices),
                new PropertyByName<ProductImportModel>("ManageInventoryMethod", p => p.ManageInventoryMethod),
                new PropertyByName<ProductImportModel>("ProductAvailabilityRange", p => p.ProductAvailabilityRange),
                new PropertyByName<ProductImportModel>("UseMultipleWarehouses", p => p.UseMultipleWarehouses),
                new PropertyByName<ProductImportModel>("WarehouseId", p => p.WarehouseId),
                new PropertyByName<ProductImportModel>("StockQuantity", p => p.StockQuantity),
                new PropertyByName<ProductImportModel>("DisplayStockAvailability", p => p.DisplayStockAvailability),
                new PropertyByName<ProductImportModel>("DisplayStockQuantity", p => p.DisplayStockQuantity),
                new PropertyByName<ProductImportModel>("MinStockQuantity", p => p.MinStockQuantity),
                new PropertyByName<ProductImportModel>("LowStockActivity", p => p.LowStockActivity),
                new PropertyByName<ProductImportModel>("NotifyAdminForQuantityBelow", p => p.NotifyAdminForQuantityBelow),
                new PropertyByName<ProductImportModel>("BackorderMode", p => p.BackorderMode),
                new PropertyByName<ProductImportModel>("AllowBackInStockSubscriptions", p => p.AllowBackInStockSubscriptions),
                new PropertyByName<ProductImportModel>("OrderMinimumQuantity", p => p.OrderMinimumQuantity),
                new PropertyByName<ProductImportModel>("OrderMaximumQuantity", p => p.OrderMaximumQuantity),
                new PropertyByName<ProductImportModel>("AllowedQuantities", p => p.AllowedQuantities),
                new PropertyByName<ProductImportModel>("AllowAddingOnlyExistingAttributeCombinations", p => p.AllowAddingOnlyExistingAttributeCombinations),
                new PropertyByName<ProductImportModel>("NotReturnable", p => p.NotReturnable),
                new PropertyByName<ProductImportModel>("DisableBuyButton", p => p.DisableBuyButton),
                new PropertyByName<ProductImportModel>("DisableWishlistButton", p => p.DisableWishlistButton),
                new PropertyByName<ProductImportModel>("AvailableForPreOrder", p => p.AvailableForPreOrder),
                new PropertyByName<ProductImportModel>("PreOrderAvailabilityStartDateTimeUtc", p => p.PreOrderAvailabilityStartDateTimeUtc),
                new PropertyByName<ProductImportModel>("CallForPrice", p => p.CallForPrice),
                new PropertyByName<ProductImportModel>("Price", p => p.Price),
                new PropertyByName<ProductImportModel>("OldPrice", p => p.OldPrice),
                new PropertyByName<ProductImportModel>("ProductCost", p => p.ProductCost),
                new PropertyByName<ProductImportModel>("CustomerEntersPrice", p => p.CustomerEntersPrice),
                new PropertyByName<ProductImportModel>("MinimumCustomerEnteredPrice", p => p.MinimumCustomerEnteredPrice),
                new PropertyByName<ProductImportModel>("MaximumCustomerEnteredPrice", p => p.MaximumCustomerEnteredPrice),
                new PropertyByName<ProductImportModel>("BasepriceEnabled", p => p.BasepriceEnabled),
                new PropertyByName<ProductImportModel>("BasepriceAmount", p => p.BasepriceAmount),
                new PropertyByName<ProductImportModel>("BasepriceUnit", p => p.BasepriceUnit),
                new PropertyByName<ProductImportModel>("BasepriceBaseAmount", p => p.BasepriceBaseAmount),
                new PropertyByName<ProductImportModel>("BasepriceBaseUnit", p => p.BasepriceBaseUnit),
                new PropertyByName<ProductImportModel>("MarkAsNew", p => p.MarkAsNew),
                new PropertyByName<ProductImportModel>("MarkAsNewStartDateTimeUtc", p => p.MarkAsNewStartDateTimeUtc),
                new PropertyByName<ProductImportModel>("MarkAsNewEndDateTimeUtc", p => p.MarkAsNewEndDateTimeUtc),
                new PropertyByName<ProductImportModel>("Weight", p => p.Weight),
                new PropertyByName<ProductImportModel>("Length", p => p.Length),
                new PropertyByName<ProductImportModel>("Width", p => p.Width),
                new PropertyByName<ProductImportModel>("Height", p => p.Height),
                new PropertyByName<ProductImportModel>("Categories", p => p.Categories),
                new PropertyByName<ProductImportModel>("Manufacturers", p => p.Manufacturers),
                new PropertyByName<ProductImportModel>("RelatedProducts", p => p.RelatedProducts),
                new PropertyByName<ProductImportModel>("ProductTags", p => p.ProductTags),
                new PropertyByName<ProductImportModel>("Picture1", p => p.Picture1),
                new PropertyByName<ProductImportModel>("Picture2", p => p.Picture2),
                new PropertyByName<ProductImportModel>("Picture3", p => p.Picture3),
                new PropertyByName<ProductImportModel>("IsLimitedToStores", p => p.IsLimitedToStores)
            };

            var newStream = new MemoryStream(await new PropertyManager<ProductImportModel>(ExportProperties, _catalogSettings).ExportToXlsxAsync(ProductsInXlsx));
            await ImportProductsFromXlsxAsync(newStream);
        }

        /// <summary>
        /// Import products from XLSX file
        /// </summary>
        /// <param name="stream">Stream</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        public virtual async Task ImportProductsFromXlsxAsync(Stream stream)
        {
            using var workbook = new XLWorkbook(stream);
            // get the first worksheet in the workbook
            var worksheet = workbook.Worksheets.FirstOrDefault();
            if (worksheet == null)
                throw new NopException("No worksheet found");

            var downloadedFiles = new List<string>();

            var metadata = await PrepareImportProductDataAsync(worksheet);

            if (_catalogSettings.ExportImportSplitProductsFile && metadata.CountProductsInFile > _catalogSettings.ExportImportProductsCountInOneFile)
            {
                await ImportProductsFromSplitedXlsxAsync(worksheet, metadata);
                return;
            }

            //performance optimization, load all products by SKU in one SQL request
            var currentVendor = await _workContext.GetCurrentVendorAsync();
            var allProductsBySku = await _productService.GetProductsBySkuAsync(metadata.AllSku.ToArray(), currentVendor?.Id ?? 0);

            //validate maximum number of products per vendor
            if (_vendorSettings.MaximumProductNumber > 0 &&
                currentVendor != null)
            {
                var newProductsCount = metadata.CountProductsInFile - allProductsBySku.Count;
                if (await _productService.GetNumberOfProductsByVendorIdAsync(currentVendor.Id) + newProductsCount > _vendorSettings.MaximumProductNumber)
                    throw new ArgumentException(string.Format(await _localizationService.GetResourceAsync("Admin.Catalog.Products.ExceededMaximumNumber"), _vendorSettings.MaximumProductNumber));
            }

            //performance optimization, load all categories IDs for products in one SQL request
            var allProductsCategoryIds = await _categoryService.GetProductCategoryIdsAsync(allProductsBySku.Select(p => p.Id).ToArray());

            #region Multi-Tenant Plugin
            var _currentStoreId = 0;
            var currentStoreId = _storeMappingService.GetStoreIdByEntityId((await _workContext.GetCurrentCustomerAsync()).Id, "Stores").FirstOrDefault();
            if (currentStoreId > 0)
            {
                _currentStoreId = currentStoreId;
            }
            #endregion

            //performance optimization, load all categories in one SQL request
            Dictionary<CategoryKey, Category> allCategories;
            try
            {
                #region Multi-Tenant Plugin
                var allCategoryList = await _categoryService.GetAllCategoriesAsync(storeId: _currentStoreId, showHidden: true);
                #endregion

                allCategories = await allCategoryList
                    .ToDictionaryAwaitAsync(async c => await CategoryKey.CreateCategoryKeyAsync(c, _categoryService, allCategoryList, _storeMappingService), c => new ValueTask<Category>(c));
            }
            catch (ArgumentException)
            {
                //categories with the same name are not supported in the same category level
                throw new ArgumentException(await _localizationService.GetResourceAsync("Admin.Catalog.Products.Import.CategoriesWithSameNameNotSupported"));
            }

            //performance optimization, load all manufacturers IDs for products in one SQL request
            var allProductsManufacturerIds = await _manufacturerService.GetProductManufacturerIdsAsync(allProductsBySku.Select(p => p.Id).ToArray());

            //performance optimization, load all manufacturers in one SQL request
            #region Multi-Tenant Plugin
            var allManufacturers = await _manufacturerService.GetAllManufacturersAsync(storeId: _currentStoreId, showHidden: true);
            #endregion

            //performance optimization, load all stores in one SQL request
            #region Multi-Tenant Plugin

            //stores
            var allStores = await _storeService.GetAllStoresByEntityNameAsync((await _workContext.GetCurrentCustomerAsync()).Id, "Stores");
            if (allStores.Count <= 0)
            {
                allStores = await _storeService.GetAllStoresAsync();
            }

            #endregion

            //product to import images
            var productPictureMetadata = new List<ProductPictureMetadata>();

            Product lastLoadedProduct = null;
            var typeOfExportedAttribute = ExportedAttributeType.NotSpecified;

            for (var iRow = 2; iRow < metadata.EndRow; iRow++)
            {
                //imports product attributes
                if (worksheet.Row(iRow).OutlineLevel != 0)
                {
                    if (lastLoadedProduct == null)
                        continue;

                    var newTypeOfExportedAttribute = GetTypeOfExportedAttribute(worksheet, metadata.ProductAttributeManager, metadata.SpecificationAttributeManager, iRow);

                    //skip caption row
                    if (newTypeOfExportedAttribute != ExportedAttributeType.NotSpecified &&
                        newTypeOfExportedAttribute != typeOfExportedAttribute)
                    {
                        typeOfExportedAttribute = newTypeOfExportedAttribute;
                        continue;
                    }

                    switch (typeOfExportedAttribute)
                    {
                        case ExportedAttributeType.ProductAttribute:
                            await ImportProductAttributeAsync(metadata.ProductAttributeManager, lastLoadedProduct);
                            break;
                        case ExportedAttributeType.SpecificationAttribute:
                            await ImportSpecificationAttributeAsync(metadata.SpecificationAttributeManager, lastLoadedProduct);
                            break;
                        case ExportedAttributeType.NotSpecified:
                        default:
                            continue;
                    }

                    continue;
                }

                metadata.Manager.ReadFromXlsx(worksheet, iRow);

                var product = metadata.SkuCellNum > 0 ? allProductsBySku.FirstOrDefault(p => p.Sku == metadata.Manager.GetProperty("SKU").StringValue) : null;

                var isNew = product == null;

                product ??= new Product();

                #region Multi-Tenant Plugin
                var _storeMappingService = Nop.Core.Infrastructure.EngineContext.Current.Resolve<Nop.Services.Stores.IStoreMappingService>();
                if (product != null && !await _storeMappingService.AuthorizeAsync(product) && !await _storeMappingService.IsAdminStore())
                {
                    return;
                }
                #endregion

                //some of previous values
                var previousStockQuantity = product.StockQuantity;
                var previousWarehouseId = product.WarehouseId;

                if (isNew)
                    product.CreatedOnUtc = DateTime.UtcNow;

                foreach (var property in metadata.Manager.GetProperties)
                {
                    switch (property.PropertyName)
                    {
                        case "ProductType":
                            product.ProductTypeId = property.IntValue;
                            break;
                        case "ParentGroupedProductId":
                            product.ParentGroupedProductId = property.IntValue;
                            break;
                        case "VisibleIndividually":
                            product.VisibleIndividually = property.BooleanValue;
                            break;
                        case "Name":
                            product.Name = property.StringValue;
                            break;
                        case "ShortDescription":
                            product.ShortDescription = property.StringValue;
                            break;
                        case "FullDescription":
                            product.FullDescription = property.StringValue;
                            break;
                        case "Vendor":
                            //vendor can't change this field
                            if (currentVendor == null)
                                product.VendorId = property.IntValue;
                            break;
                        case "ProductTemplate":
                            product.ProductTemplateId = property.IntValue;
                            break;
                        case "ShowOnHomepage":
                            //vendor can't change this field
                            if (currentVendor == null)
                                product.ShowOnHomepage = property.BooleanValue;
                            break;
                        case "DisplayOrder":
                            //vendor can't change this field
                            if (currentVendor == null)
                                product.DisplayOrder = property.IntValue;
                            break;
                        case "MetaKeywords":
                            product.MetaKeywords = property.StringValue;
                            break;
                        case "MetaDescription":
                            product.MetaDescription = property.StringValue;
                            break;
                        case "MetaTitle":
                            product.MetaTitle = property.StringValue;
                            break;
                        case "AllowCustomerReviews":
                            product.AllowCustomerReviews = property.BooleanValue;
                            break;
                        case "Published":
                            product.Published = property.BooleanValue;
                            break;
                        case "SKU":
                            product.Sku = property.StringValue;
                            break;
                        case "ManufacturerPartNumber":
                            product.ManufacturerPartNumber = property.StringValue;
                            break;
                        case "Gtin":
                            product.Gtin = property.StringValue;
                            break;
                        case "IsGiftCard":
                            product.IsGiftCard = property.BooleanValue;
                            break;
                        case "GiftCardType":
                            product.GiftCardTypeId = property.IntValue;
                            break;
                        case "OverriddenGiftCardAmount":
                            product.OverriddenGiftCardAmount = property.DecimalValue;
                            break;
                        case "RequireOtherProducts":
                            product.RequireOtherProducts = property.BooleanValue;
                            break;
                        case "RequiredProductIds":
                            product.RequiredProductIds = property.StringValue;
                            break;
                        case "AutomaticallyAddRequiredProducts":
                            product.AutomaticallyAddRequiredProducts = property.BooleanValue;
                            break;
                        case "IsDownload":
                            product.IsDownload = property.BooleanValue;
                            break;
                        case "DownloadId":
                            product.DownloadId = property.IntValue;
                            break;
                        case "UnlimitedDownloads":
                            product.UnlimitedDownloads = property.BooleanValue;
                            break;
                        case "MaxNumberOfDownloads":
                            product.MaxNumberOfDownloads = property.IntValue;
                            break;
                        case "DownloadActivationType":
                            product.DownloadActivationTypeId = property.IntValue;
                            break;
                        case "HasSampleDownload":
                            product.HasSampleDownload = property.BooleanValue;
                            break;
                        case "SampleDownloadId":
                            product.SampleDownloadId = property.IntValue;
                            break;
                        case "HasUserAgreement":
                            product.HasUserAgreement = property.BooleanValue;
                            break;
                        case "UserAgreementText":
                            product.UserAgreementText = property.StringValue;
                            break;
                        case "IsRecurring":
                            product.IsRecurring = property.BooleanValue;
                            break;
                        case "RecurringCycleLength":
                            product.RecurringCycleLength = property.IntValue;
                            break;
                        case "RecurringCyclePeriod":
                            product.RecurringCyclePeriodId = property.IntValue;
                            break;
                        case "RecurringTotalCycles":
                            product.RecurringTotalCycles = property.IntValue;
                            break;
                        case "IsRental":
                            product.IsRental = property.BooleanValue;
                            break;
                        case "RentalPriceLength":
                            product.RentalPriceLength = property.IntValue;
                            break;
                        case "RentalPricePeriod":
                            product.RentalPricePeriodId = property.IntValue;
                            break;
                        case "IsShipEnabled":
                            product.IsShipEnabled = property.BooleanValue;
                            break;
                        case "IsFreeShipping":
                            product.IsFreeShipping = property.BooleanValue;
                            break;
                        case "ShipSeparately":
                            product.ShipSeparately = property.BooleanValue;
                            break;
                        case "AdditionalShippingCharge":
                            product.AdditionalShippingCharge = property.DecimalValue;
                            break;
                        case "DeliveryDate":
                            product.DeliveryDateId = property.IntValue;
                            break;
                        case "IsTaxExempt":
                            product.IsTaxExempt = property.BooleanValue;
                            break;
                        case "TaxCategory":
                            product.TaxCategoryId = property.IntValue;
                            break;
                        case "IsTelecommunicationsOrBroadcastingOrElectronicServices":
                            product.IsTelecommunicationsOrBroadcastingOrElectronicServices = property.BooleanValue;
                            break;
                        case "ManageInventoryMethod":
                            product.ManageInventoryMethodId = property.IntValue;
                            break;
                        case "ProductAvailabilityRange":
                            product.ProductAvailabilityRangeId = property.IntValue;
                            break;
                        case "UseMultipleWarehouses":
                            product.UseMultipleWarehouses = property.BooleanValue;
                            break;
                        case "WarehouseId":
                            product.WarehouseId = property.IntValue;
                            break;
                        case "StockQuantity":
                            product.StockQuantity = property.IntValue;
                            break;
                        case "DisplayStockAvailability":
                            product.DisplayStockAvailability = property.BooleanValue;
                            break;
                        case "DisplayStockQuantity":
                            product.DisplayStockQuantity = property.BooleanValue;
                            break;
                        case "MinStockQuantity":
                            product.MinStockQuantity = property.IntValue;
                            break;
                        case "LowStockActivity":
                            product.LowStockActivityId = property.IntValue;
                            break;
                        case "NotifyAdminForQuantityBelow":
                            product.NotifyAdminForQuantityBelow = property.IntValue;
                            break;
                        case "BackorderMode":
                            product.BackorderModeId = property.IntValue;
                            break;
                        case "AllowBackInStockSubscriptions":
                            product.AllowBackInStockSubscriptions = property.BooleanValue;
                            break;
                        case "OrderMinimumQuantity":
                            product.OrderMinimumQuantity = property.IntValue;
                            break;
                        case "OrderMaximumQuantity":
                            product.OrderMaximumQuantity = property.IntValue;
                            break;
                        case "AllowedQuantities":
                            product.AllowedQuantities = property.StringValue;
                            break;
                        case "AllowAddingOnlyExistingAttributeCombinations":
                            product.AllowAddingOnlyExistingAttributeCombinations = property.BooleanValue;
                            break;
                        case "NotReturnable":
                            product.NotReturnable = property.BooleanValue;
                            break;
                        case "DisableBuyButton":
                            product.DisableBuyButton = property.BooleanValue;
                            break;
                        case "DisableWishlistButton":
                            product.DisableWishlistButton = property.BooleanValue;
                            break;
                        case "AvailableForPreOrder":
                            product.AvailableForPreOrder = property.BooleanValue;
                            break;
                        case "PreOrderAvailabilityStartDateTimeUtc":
                            product.PreOrderAvailabilityStartDateTimeUtc = property.DateTimeNullable;
                            break;
                        case "CallForPrice":
                            product.CallForPrice = property.BooleanValue;
                            break;
                        case "Price":
                            product.Price = property.DecimalValue;
                            break;
                        case "OldPrice":
                            product.OldPrice = property.DecimalValue;
                            break;
                        case "ProductCost":
                            product.ProductCost = property.DecimalValue;
                            break;
                        case "CustomerEntersPrice":
                            product.CustomerEntersPrice = property.BooleanValue;
                            break;
                        case "MinimumCustomerEnteredPrice":
                            product.MinimumCustomerEnteredPrice = property.DecimalValue;
                            break;
                        case "MaximumCustomerEnteredPrice":
                            product.MaximumCustomerEnteredPrice = property.DecimalValue;
                            break;
                        case "BasepriceEnabled":
                            product.BasepriceEnabled = property.BooleanValue;
                            break;
                        case "BasepriceAmount":
                            product.BasepriceAmount = property.DecimalValue;
                            break;
                        case "BasepriceUnit":
                            product.BasepriceUnitId = property.IntValue;
                            break;
                        case "BasepriceBaseAmount":
                            product.BasepriceBaseAmount = property.DecimalValue;
                            break;
                        case "BasepriceBaseUnit":
                            product.BasepriceBaseUnitId = property.IntValue;
                            break;
                        case "MarkAsNew":
                            product.MarkAsNew = property.BooleanValue;
                            break;
                        case "MarkAsNewStartDateTimeUtc":
                            product.MarkAsNewStartDateTimeUtc = property.DateTimeNullable;
                            break;
                        case "MarkAsNewEndDateTimeUtc":
                            product.MarkAsNewEndDateTimeUtc = property.DateTimeNullable;
                            break;
                        case "Weight":
                            product.Weight = property.DecimalValue;
                            break;
                        case "Length":
                            product.Length = property.DecimalValue;
                            break;
                        case "Width":
                            product.Width = property.DecimalValue;
                            break;
                        case "Height":
                            product.Height = property.DecimalValue;
                            break;
                        case "IsLimitedToStores":
                            product.LimitedToStores = property.BooleanValue;
                            break;
                    }
                }

                //set some default values if not specified
                if (isNew && metadata.Properties.All(p => p.PropertyName != "ProductType"))
                    product.ProductType = ProductType.SimpleProduct;
                if (isNew && metadata.Properties.All(p => p.PropertyName != "VisibleIndividually"))
                    product.VisibleIndividually = true;
                if (isNew && metadata.Properties.All(p => p.PropertyName != "Published"))
                    product.Published = true;

                //sets the current vendor for the new product
                if (isNew && currentVendor != null)
                    product.VendorId = currentVendor.Id;

                product.UpdatedOnUtc = DateTime.UtcNow;

                if (isNew){
                    // #region Multi-Tenant Plugin
                    // product.LimitedToStores = true;
                    // #endregion
                    
                    await _productService.InsertProductAsync(product);
                    
                    #region Multi-Tenant Plugin
                    if (await _storeMappingService.CurrentStore() > 0)
                    {
                        await _storeMappingService.InsertStoreMappingAsync(product, await _storeMappingService.CurrentStore());
                    }
                    #endregion
                }
                else
                    await _productService.UpdateProductAsync(product);

                //quantity change history
                if (isNew || previousWarehouseId == product.WarehouseId)
                {
                    await _productService.AddStockQuantityHistoryEntryAsync(product, product.StockQuantity - previousStockQuantity, product.StockQuantity,
                        product.WarehouseId, await _localizationService.GetResourceAsync("Admin.StockQuantityHistory.Messages.ImportProduct.Edit"));
                }
                //warehouse is changed 
                else
                {
                    //compose a message
                    var oldWarehouseMessage = string.Empty;
                    if (previousWarehouseId > 0)
                    {
                        var oldWarehouse = await _shippingService.GetWarehouseByIdAsync(previousWarehouseId);
                        if (oldWarehouse != null)
                            oldWarehouseMessage = string.Format(await _localizationService.GetResourceAsync("Admin.StockQuantityHistory.Messages.EditWarehouse.Old"), oldWarehouse.Name);
                    }

                    var newWarehouseMessage = string.Empty;
                    if (product.WarehouseId > 0)
                    {
                        var newWarehouse = await _shippingService.GetWarehouseByIdAsync(product.WarehouseId);
                        if (newWarehouse != null)
                            newWarehouseMessage = string.Format(await _localizationService.GetResourceAsync("Admin.StockQuantityHistory.Messages.EditWarehouse.New"), newWarehouse.Name);
                    }

                    var message = string.Format(await _localizationService.GetResourceAsync("Admin.StockQuantityHistory.Messages.ImportProduct.EditWarehouse"), oldWarehouseMessage, newWarehouseMessage);

                    //record history
                    await _productService.AddStockQuantityHistoryEntryAsync(product, -previousStockQuantity, 0, previousWarehouseId, message);
                    await _productService.AddStockQuantityHistoryEntryAsync(product, product.StockQuantity, product.StockQuantity, product.WarehouseId, message);
                }

                var tempProperty = metadata.Manager.GetProperty("SeName");

                //search engine name
                var seName = tempProperty?.StringValue ?? (isNew ? string.Empty : await _urlRecordService.GetSeNameAsync(product, 0));
                await _urlRecordService.SaveSlugAsync(product, await _urlRecordService.ValidateSeNameAsync(product, seName, product.Name, true), 0);

                tempProperty = metadata.Manager.GetProperty("Categories");

                if (tempProperty != null)
                {
                    var categoryList = tempProperty.StringValue;

                    //category mappings
                    var categories = isNew || !allProductsCategoryIds.ContainsKey(product.Id) ? Array.Empty<int>() : allProductsCategoryIds[product.Id];

                    var storesIds = product.LimitedToStores
                        ? (await _storeMappingService.GetStoresIdsWithAccessAsync(product)).ToList()
                        : new List<int>();

                    var importedCategories = await categoryList.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(categoryName => new CategoryKey(categoryName, storesIds))
                        .SelectAwait(async categoryKey =>
                        {
                            var rez = (allCategories.ContainsKey(categoryKey) ? allCategories[categoryKey].Id : allCategories.Values.FirstOrDefault(c => c.Name == categoryKey.Key)?.Id) ??
                                      allCategories.FirstOrDefault(p =>
                                    p.Key.Key.Equals(categoryKey.Key, StringComparison.InvariantCultureIgnoreCase))
                                .Value?.Id;

                            if (!rez.HasValue && int.TryParse(categoryKey.Key, out var id)) 
                                rez = id;

                            if (!rez.HasValue)
                                //database doesn't contain the imported category
                                throw new ArgumentException(string.Format(await _localizationService.GetResourceAsync("Admin.Catalog.Products.Import.DatabaseNotContainCategory"), categoryKey.Key));

                            return rez.Value;
                        }).ToListAsync();

                    foreach (var categoryId in importedCategories)
                    {
                        if (categories.Any(c => c == categoryId))
                            continue;

                        var productCategory = new ProductCategory
                        {
                            ProductId = product.Id,
                            CategoryId = categoryId,
                            IsFeaturedProduct = false,
                            DisplayOrder = 1
                        };
                        await _categoryService.InsertProductCategoryAsync(productCategory);
                    }

                    //delete product categories
                    var deletedProductCategories = await categories.Where(categoryId => !importedCategories.Contains(categoryId))
                        .SelectAwait(async categoryId => (await _categoryService.GetProductCategoriesByProductIdAsync(product.Id, true)).FirstOrDefault(pc => pc.CategoryId == categoryId)).Where(pc=>pc != null).ToListAsync();

                    foreach (var deletedProductCategory in deletedProductCategories) 
                        await _categoryService.DeleteProductCategoryAsync(deletedProductCategory);
                }

                tempProperty = metadata.Manager.GetProperty("Manufacturers");
                if (tempProperty != null)
                {
                    var manufacturerList = tempProperty.StringValue;

                    //manufacturer mappings
                    var manufacturers = isNew || !allProductsManufacturerIds.ContainsKey(product.Id) ? Array.Empty<int>() : allProductsManufacturerIds[product.Id];
                    var importedManufacturers = manufacturerList.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(x => allManufacturers.FirstOrDefault(m => m.Name == x.Trim())?.Id ?? int.Parse(x.Trim())).ToList();
                    foreach (var manufacturerId in importedManufacturers)
                    {
                        if (manufacturers.Any(c => c == manufacturerId))
                            continue;

                        var productManufacturer = new ProductManufacturer
                        {
                            ProductId = product.Id,
                            ManufacturerId = manufacturerId,
                            IsFeaturedProduct = false,
                            DisplayOrder = 1
                        };
                        await _manufacturerService.InsertProductManufacturerAsync(productManufacturer);
                    }

                    //delete product manufacturers
                    var deletedProductsManufacturers = await manufacturers.Where(manufacturerId => !importedManufacturers.Contains(manufacturerId))
                        .SelectAwait(async manufacturerId => (await _manufacturerService.GetProductManufacturersByProductIdAsync(product.Id)).First(pc => pc.ManufacturerId == manufacturerId)).ToListAsync();
                    foreach (var deletedProductManufacturer in deletedProductsManufacturers) 
                        await _manufacturerService.DeleteProductManufacturerAsync(deletedProductManufacturer);
                }

                tempProperty = metadata.Manager.GetProperty("ProductTags");
                if (tempProperty != null)
                {
                    var productTags = tempProperty.StringValue.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();

                    //searching existing product tags by their id
                    var productTagIds = productTags.Where(pt => int.TryParse(pt, out var _)).Select(int.Parse);

                    var productTagsByIds = (await _productTagService.GetAllProductTagsByProductIdAsync(product.Id)).Where(pt => productTagIds.Contains(pt.Id)).ToList();

                    productTags.AddRange(productTagsByIds.Select(pt => pt.Name));
                    var filter = productTagsByIds.Select(pt => pt.Id.ToString()).ToList();

                    //product tag mappings
                    await _productTagService.UpdateProductTagsAsync(product, productTags.Where(pt => !filter.Contains(pt)).ToArray());
                }

                tempProperty = metadata.Manager.GetProperty("LimitedToStores");
                if (tempProperty != null)
                {
                    var limitedToStoresList = tempProperty.StringValue;

                    var importedStores = product.LimitedToStores ? limitedToStoresList.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(x => allStores.FirstOrDefault(store => store.Name == x.Trim())?.Id ?? int.Parse(x.Trim())).ToList() : new List<int>();

                    await _productService.UpdateProductStoreMappingsAsync(product, importedStores);
                }

                var picture1 = await DownloadFileAsync(metadata.Manager.GetProperty("Picture1")?.StringValue, downloadedFiles);
                var picture2 = await DownloadFileAsync(metadata.Manager.GetProperty("Picture2")?.StringValue, downloadedFiles);
                var picture3 = await DownloadFileAsync(metadata.Manager.GetProperty("Picture3")?.StringValue, downloadedFiles);

                productPictureMetadata.Add(new ProductPictureMetadata
                {
                    ProductItem = product,
                    Picture1Path = picture1,
                    Picture2Path = picture2,
                    Picture3Path = picture3,
                    IsNew = isNew
                });

                lastLoadedProduct = product;

                //update "HasTierPrices" and "HasDiscountsApplied" properties
                //_productService.UpdateHasTierPricesProperty(product);
                //_productService.UpdateHasDiscountsApplied(product);
            }

            if (_mediaSettings.ImportProductImagesUsingHash && await _pictureService.IsStoreInDbAsync())
                await ImportProductImagesUsingHashAsync(productPictureMetadata, allProductsBySku);
            else
                await ImportProductImagesUsingServicesAsync(productPictureMetadata);

            foreach (var downloadedFile in downloadedFiles)
            {
                if (!_fileProvider.FileExists(downloadedFile))
                    continue;

                try
                {
                    _fileProvider.DeleteFile(downloadedFile);
                }
                catch
                {
                    // ignored
                }
            }

            //activity log
            await _customerActivityService.InsertActivityAsync("ImportProducts", string.Format(await _localizationService.GetResourceAsync("ActivityLog.ImportProducts"), metadata.CountProductsInFile));
        }

        /// <summary>
        /// Import newsletter subscribers from TXT file
        /// </summary>
        /// <param name="stream">Stream</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the number of imported subscribers
        /// </returns>
        public virtual async Task<int> ImportNewsletterSubscribersFromTxtAsync(Stream stream)
        {
            var count = 0;
            using (var reader = new StreamReader(stream))
                while (!reader.EndOfStream)
                {
                    var line = await reader.ReadLineAsync();
                    if (string.IsNullOrWhiteSpace(line))
                        continue;
                    var tmp = line.Split(',');

                    if (tmp.Length > 3)
                        throw new NopException("Wrong file format");

                    var isActive = true;

                    var store = await _storeContext.GetCurrentStoreAsync();
                    var storeId = store.Id;

                    //"email" field specified
                    var email = tmp[0].Trim();

                    if (!CommonHelper.IsValidEmail(email))
                        continue;

                    //"active" field specified
                    if (tmp.Length >= 2)
                        isActive = bool.Parse(tmp[1].Trim());

                    //"storeId" field specified
                    if (tmp.Length == 3)
                        storeId = int.Parse(tmp[2].Trim());

                    //import
                    var subscription = await _newsLetterSubscriptionService.GetNewsLetterSubscriptionByEmailAndStoreIdAsync(email, storeId);
                    if (subscription != null)
                    {
                        subscription.Email = email;
                        subscription.Active = isActive;
                        await _newsLetterSubscriptionService.UpdateNewsLetterSubscriptionAsync(subscription);
                    }
                    else
                    {
                        subscription = new NewsLetterSubscription
                        {
                            Active = isActive,
                            CreatedOnUtc = DateTime.UtcNow,
                            Email = email,
                            StoreId = storeId,
                            NewsLetterSubscriptionGuid = Guid.NewGuid()
                        };
                        await _newsLetterSubscriptionService.InsertNewsLetterSubscriptionAsync(subscription);
                    }

                    count++;
                }

            return count;
        }

        /// <summary>
        /// Import states from TXT file
        /// </summary>
        /// <param name="stream">Stream</param>
        /// <param name="writeLog">Indicates whether to add logging</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the number of imported states
        /// </returns>
        public virtual async Task<int> ImportStatesFromTxtAsync(Stream stream, bool writeLog = true)
        {
            var count = 0;
            using (var reader = new StreamReader(stream))
            {
                while (!reader.EndOfStream)
                {
                    var line = await reader.ReadLineAsync();
                    if (string.IsNullOrWhiteSpace(line))
                        continue;
                    var tmp = line.Split(',');

                    if (tmp.Length != 5)
                        throw new NopException("Wrong file format");

                    //parse
                    var countryTwoLetterIsoCode = tmp[0].Trim();
                    var name = tmp[1].Trim();
                    var abbreviation = tmp[2].Trim();
                    var published = bool.Parse(tmp[3].Trim());
                    var displayOrder = int.Parse(tmp[4].Trim());

                    var country = await _countryService.GetCountryByTwoLetterIsoCodeAsync(countryTwoLetterIsoCode);
                    if (country == null)
                    {
                        //country cannot be loaded. skip
                        continue;
                    }

                    //import
                    var states = await _stateProvinceService.GetStateProvincesByCountryIdAsync(country.Id, showHidden: true);
                    var state = states.FirstOrDefault(x => x.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase));

                    if (state != null)
                    {
                        state.Abbreviation = abbreviation;
                        state.Published = published;
                        state.DisplayOrder = displayOrder;
                        await _stateProvinceService.UpdateStateProvinceAsync(state);
                    }
                    else
                    {
                        state = new StateProvince
                        {
                            CountryId = country.Id,
                            Name = name,
                            Abbreviation = abbreviation,
                            Published = published,
                            DisplayOrder = displayOrder
                        };
                        await _stateProvinceService.InsertStateProvinceAsync(state);
                    }

                    count++;
                }
            }

            //activity log
            if (writeLog)
            {
                await _customerActivityService.InsertActivityAsync("ImportStates",
                    string.Format(await _localizationService.GetResourceAsync("ActivityLog.ImportStates"), count));
            }

            return count;
        }

        /// <summary>
        /// Import manufacturers from XLSX file
        /// </summary>
        /// <param name="stream">Stream</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        public virtual async Task ImportManufacturersFromXlsxAsync(Stream stream)
        {
            using var workbook = new XLWorkbook(stream);
            // get the first worksheet in the workbook
            var worksheet = workbook.Worksheets.FirstOrDefault();
            if (worksheet == null)
                throw new NopException("No worksheet found");

            //the columns
            var properties = GetPropertiesByExcelCells<Manufacturer>(worksheet);

            var manager = new PropertyManager<Manufacturer>(properties, _catalogSettings);

            var iRow = 2;
            var setSeName = properties.Any(p => p.PropertyName == "SeName");

            var customer = await _workContext.GetCurrentCustomerAsync();
            var IsLimitedToStores = !await _customerService.IsAdminAsync(customer);

            while (true)
            {
                var allColumnsAreEmpty = manager.GetProperties
                    .Select(property => worksheet.Row(iRow).Cell(property.PropertyOrderPosition))
                    .All(cell => cell?.Value == null || string.IsNullOrEmpty(cell.Value.ToString()));

                if (allColumnsAreEmpty)
                    break;

                manager.ReadFromXlsx(worksheet, iRow);

                var manufacturer = await _manufacturerService.GetManufacturerByIdAsync(manager.GetProperty("Id").IntValue);

                var isNew = manufacturer == null;

                manufacturer ??= new Manufacturer();

                #region Multi-Tenant Plugin

                var _storeMappingService = Nop.Core.Infrastructure.EngineContext.Current.Resolve<Nop.Services.Stores.IStoreMappingService>();
                if (manufacturer != null && !await _storeMappingService.AuthorizeAsync(manufacturer) && !await _storeMappingService.IsAdminStore())
                {
                    return;
                }
                #endregion

                if (isNew)
                {
                    manufacturer.CreatedOnUtc = DateTime.UtcNow;

                    //default values
                    manufacturer.PageSize = _catalogSettings.DefaultManufacturerPageSize;
                    manufacturer.PageSizeOptions = _catalogSettings.DefaultManufacturerPageSizeOptions;
                    manufacturer.Published = true;
                    manufacturer.AllowCustomersToSelectPageSize = true;
                }

                var seName = string.Empty;

                foreach (var property in manager.GetProperties)
                {
                    switch (property.PropertyName)
                    {
                        case "Name":
                            manufacturer.Name = property.StringValue;
                            break;
                        case "Description":
                            manufacturer.Description = property.StringValue;
                            break;
                        case "ManufacturerTemplateId":
                            manufacturer.ManufacturerTemplateId = property.IntValue;
                            break;
                        case "MetaKeywords":
                            manufacturer.MetaKeywords = property.StringValue;
                            break;
                        case "MetaDescription":
                            manufacturer.MetaDescription = property.StringValue;
                            break;
                        case "MetaTitle":
                            manufacturer.MetaTitle = property.StringValue;
                            break;
                        case "Picture":
                            var picture = await LoadPictureAsync(manager.GetProperty("Picture").StringValue, manufacturer.Name, isNew ? null : (int?)manufacturer.PictureId);

                            if (picture != null)
                                manufacturer.PictureId = picture.Id;

                            break;
                        case "PageSize":
                            manufacturer.PageSize = property.IntValue;
                            break;
                        case "AllowCustomersToSelectPageSize":
                            manufacturer.AllowCustomersToSelectPageSize = property.BooleanValue;
                            break;
                        case "PageSizeOptions":
                            manufacturer.PageSizeOptions = property.StringValue;
                            break;
                        case "PriceRangeFiltering":
                            manufacturer.PriceRangeFiltering = property.BooleanValue;
                            break;
                        case "PriceFrom":
                            manufacturer.PriceFrom = property.DecimalValue;
                            break;
                        case "PriceTo":
                            manufacturer.PriceTo = property.DecimalValue;
                            break;
                        case "AutomaticallyCalculatePriceRange":
                            manufacturer.ManuallyPriceRange = property.BooleanValue;
                            break;
                        case "Published":
                            manufacturer.Published = property.BooleanValue;
                            break;
                        case "DisplayOrder":
                            manufacturer.DisplayOrder = property.IntValue;
                            break;
                        case "SeName":
                            seName = property.StringValue;
                            break;
                    }
                }

                manufacturer.UpdatedOnUtc = DateTime.UtcNow;

                if (isNew){
                    #region Multi-Tenant Plugin
                    // manufacturer.LimitedToStores = true;
                    manufacturer.LimitedToStores = IsLimitedToStores;
                    #endregion
                    
                    await _manufacturerService.InsertManufacturerAsync(manufacturer);
                    
                    #region Multi-Tenant Plugin
                    if (await _storeMappingService.CurrentStore() > 0)
                    {
                        await _storeMappingService.InsertStoreMappingAsync(manufacturer, await _storeMappingService.CurrentStore());
                    }
                    #endregion
                }
                else
                    await _manufacturerService.UpdateManufacturerAsync(manufacturer);

                //search engine name
                if (setSeName)
                    await _urlRecordService.SaveSlugAsync(manufacturer, await _urlRecordService.ValidateSeNameAsync(manufacturer, seName, manufacturer.Name, true), 0);

                iRow++;
            }

            //activity log
            await _customerActivityService.InsertActivityAsync("ImportManufacturers",
                string.Format(await _localizationService.GetResourceAsync("ActivityLog.ImportManufacturers"), iRow - 2));
        }

        /// <summary>
        /// Import categories from XLSX file
        /// </summary>
        /// <param name="stream">Stream</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        public virtual async Task ImportCategoriesFromXlsxAsync(Stream stream)
        {
            using var workboox = new XLWorkbook(stream);
            // get the first worksheet in the workbook
            var worksheet = workboox.Worksheets.FirstOrDefault();
            if (worksheet == null)
                throw new NopException("No worksheet found");

            //the columns
            var properties = GetPropertiesByExcelCells<Category>(worksheet);

            var manager = new PropertyManager<Category>(properties, _catalogSettings);

            var iRow = 2;
            var setSeName = properties.Any(p => p.PropertyName == "SeName");

            //performance optimization, load all categories in one SQL request
            var allCategories = await (await _categoryService
                .GetAllCategoriesAsync(showHidden: true))
                .GroupByAwait(async c => await _categoryService.GetFormattedBreadCrumbAsync(c))
                .ToDictionaryAsync(c => c.Key, c => c.FirstAsync());

            var saveNextTime = new List<int>();

            while (true)
            {
                var allColumnsAreEmpty = manager.GetProperties
                    .Select(property => worksheet.Row(iRow).Cell(property.PropertyOrderPosition))
                    .All(cell => string.IsNullOrEmpty(cell?.Value?.ToString()));

                if (allColumnsAreEmpty)
                    break;

                //get category by data in xlsx file if it possible, or create new category
                var (category, isNew, currentCategoryBreadCrumb) = await GetCategoryFromXlsxAsync(manager, worksheet, iRow, allCategories);

                //update category by data in xlsx file
                var (seName, isParentCategoryExists) = await UpdateCategoryByXlsxAsync(category, manager, allCategories, isNew);

                #region Multi-Tenant Plugin
                var _storeMappingService = Nop.Core.Infrastructure.EngineContext.Current.Resolve<Nop.Services.Stores.IStoreMappingService>();
                if (category != null && !await _storeMappingService.AuthorizeAsync(category) && !await _storeMappingService.IsAdminStore())
                {
                    return;
                }
                #endregion

                if (isParentCategoryExists)
                {
                    //if parent category exists in database then save category into database
                    await SaveCategoryAsync(isNew, category, allCategories, currentCategoryBreadCrumb, setSeName, seName);
                }
                else
                {
                    //if parent category doesn't exists in database then try save category into database next time
                    saveNextTime.Add(iRow);
                }

                iRow++;
            }

            var needSave = saveNextTime.Any();

            while (needSave)
            {
                var remove = new List<int>();

                //try to save unsaved categories
                foreach (var rowId in saveNextTime)
                {
                    //get category by data in xlsx file if it possible, or create new category
                    var (category, isNew, currentCategoryBreadCrumb) = await GetCategoryFromXlsxAsync(manager, worksheet, rowId, allCategories);
                    //update category by data in xlsx file
                    var (seName, isParentCategoryExists) = await UpdateCategoryByXlsxAsync(category, manager, allCategories, isNew);

                    if (!isParentCategoryExists)
                        continue;

                    //if parent category exists in database then save category into database
                    await SaveCategoryAsync(isNew, category, allCategories, currentCategoryBreadCrumb, setSeName, seName);
                    remove.Add(rowId);
                }

                saveNextTime.RemoveAll(item => remove.Contains(item));

                needSave = remove.Any() && saveNextTime.Any();
            }

            //activity log
            await _customerActivityService.InsertActivityAsync("ImportCategories",
                string.Format(await _localizationService.GetResourceAsync("ActivityLog.ImportCategories"), iRow - 2 - saveNextTime.Count));

            if (!saveNextTime.Any())
                return;

            var categoriesName = new List<string>();

            foreach (var rowId in saveNextTime)
            {
                manager.ReadFromXlsx(worksheet, rowId);
                var name = manager.GetProperty("Code") == null ? manager.GetProperty("Name").StringValue : manager.GetProperty("Title en").StringValue + "(" + manager.GetProperty("Code").StringValue + ")";
                categoriesName.Add(name);
            }

            throw new ArgumentException(string.Format(await _localizationService.GetResourceAsync("Admin.Catalog.Categories.Import.CategoriesArentImported"), string.Join(", ", categoriesName)));
        }

        /// <summary>
        /// Import Tier Price from XLSX file
        /// </summary>
        /// <param name="stream">Stream</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        public virtual async Task ImportTierPriceFromXlsxAsync(Stream stream, bool AllStore)
        {
            using var workbook = new XLWorkbook(stream);
            // get the first worksheet in the workbook
            var PriceWS = workbook.Worksheets.FirstOrDefault(i => i.Name == "Tiers"); // Worksheet For Prices
            var RoleWS = workbook.Worksheets.FirstOrDefault(i => i.Name == "Roles"); // Worksheet For Roles

            if (PriceWS == null)
                throw new NopException("No worksheet found");
            if (RoleWS == null)
                throw new NopException("Role worksheet found");

            var AllCustomerRoles = await _customerService.GetAllCustomerRolesAsync();
            int StoreId = AllStore != true ? _storeContext.GetCurrentStore().Id : 0;

            var RoleMetaData = PrepareImportRolesForTierPricingDataAsync(RoleWS);
            var PriceMetaData = await PrepareImportPricesForTierPricingDataAsync(RoleMetaData.RoleDetails, StoreId, PriceWS);

            var TotalRecordsInserted = 1;

            List<TierPrice> NewDataTiresList = new List<TierPrice>();
            List<TierPrice> UpdateDataTiresList = new List<TierPrice>();

            List<Product> UpdateProductList = new List<Product>();

            foreach (var product in PriceMetaData.ProductList)
            {
                var ProductTierPricesDB = await _productService.GetTierPricesByProductAsync(product.Id);
                var TirePriceObject = PriceMetaData.ImportTierPriceList.FirstOrDefault(i => i.SKU == product.Sku);
                
                if(TirePriceObject == null) continue;

                List<TirePriceListObject> ListForTirePrices = new List<TirePriceListObject>();
                ListForTirePrices.Add(new TirePriceListObject(TirePriceObject.PriceForTireOne, TirePriceObject.RoleForTireOne));
                ListForTirePrices.Add(new TirePriceListObject(TirePriceObject.PriceForTireTwo, TirePriceObject.RoleForTireTwo));
                ListForTirePrices.Add(new TirePriceListObject(TirePriceObject.PriceForTireThree, TirePriceObject.RoleForTireThree));
                ListForTirePrices.Add(new TirePriceListObject(TirePriceObject.PriceForTireFour, TirePriceObject.RoleForTireFour));
                ListForTirePrices.Add(new TirePriceListObject(TirePriceObject.PriceForTireFive, TirePriceObject.RoleForTireFive));
                ListForTirePrices.Add(new TirePriceListObject(TirePriceObject.PriceForTireSix, TirePriceObject.RoleForTireSix));

                foreach (var Tire in ListForTirePrices)
                {
                    var RoleId = AllCustomerRoles.FirstOrDefault(i => i.Name == Tire.Role).Id; 
                    var tierPrice = ProductTierPricesDB.FirstOrDefault(i => i.CustomerRoleId == RoleId && i.StoreId == StoreId);

                    var isNew = tierPrice == null;

                    tierPrice ??= new TierPrice();

                    if (isNew)
                    {
                        tierPrice.ProductId = product.Id;
                        tierPrice.StoreId = StoreId;
                        tierPrice.CustomerRoleId = RoleId;
                        tierPrice.Quantity = 1;
                        tierPrice.Price = (decimal)Tire.Price;

                        await _productService.InsertTierPriceAsync(tierPrice);
                        // NewDataTiresList.Add(tierPrice);
                    }else{
                        if(tierPrice.Price != (decimal)Tire.Price){
                            tierPrice.Price = (decimal)Tire.Price;
                            await _productService.UpdateTierPriceAsync(tierPrice);
                            // UpdateDataTiresList.Add(tierPrice);
                        }
                    }
                }

                var ProductUpdate = false;
                
                if(product.Price != TirePriceObject.ProductPrice){
                    product.Price = TirePriceObject.ProductPrice;
                    ProductUpdate = true;
                }
                if(product.HasTierPrices == false){
                    product.HasTierPrices = true;
                    ProductUpdate = true;
                }

                if(ProductUpdate == true) await _productService.UpdateProductAsync(product);


                TotalRecordsInserted++;
            }

            // if(NewDataTiresList.Count != 0) await _productService.InsertTierPriceAsync(NewDataTiresList);
            // if(UpdateDataTiresList.Count != 0) await _productService.UpdateTierPriceAsync(UpdateDataTiresList);
            // if(UpdateProductList.Count != 0) await _productService.UpdateProductAsync(UpdateProductList);

            // activity log
            System.Console.WriteLine("Products Tier Prices been set = {0}", TotalRecordsInserted);
            await _customerActivityService.InsertActivityAsync("ImportProductTierPrice",
                string.Format(await _localizationService.GetResourceAsync("activitylog.importTierPrice"), TotalRecordsInserted));
        }

        #endregion

        #region Nested classes

        protected class ProductPictureMetadata
        {
            public Product ProductItem { get; set; }

            public string Picture1Path { get; set; }

            public string Picture2Path { get; set; }

            public string Picture3Path { get; set; }

            public bool IsNew { get; set; }
        }

        public class CategoryKey
        {
            /// <returns>A task that represents the asynchronous operation</returns>
            public static async Task<CategoryKey> CreateCategoryKeyAsync(Category category, ICategoryService categoryService, IList<Category> allCategories, IStoreMappingService storeMappingService)
            {
                return new CategoryKey(await categoryService.GetFormattedBreadCrumbAsync(category, allCategories), category.LimitedToStores ? (await storeMappingService.GetStoresIdsWithAccessAsync(category)).ToList() : new List<int>())
                {
                    Category = category
                };
            }

            public CategoryKey(string key, List<int> storesIds = null)
            {
                Key = key.Trim();
                StoresIds = storesIds ?? new List<int>();
            }

            public List<int> StoresIds { get; }

            public Category Category { get; private set; }

            public string Key { get; }

            public bool Equals(CategoryKey y)
            {
                if (y == null)
                    return false;

                if (Category != null && y.Category != null)
                    return Category.Id == y.Category.Id;

                if ((StoresIds.Any() || y.StoresIds.Any())
                    && (StoresIds.All(id => !y.StoresIds.Contains(id)) || y.StoresIds.All(id => !StoresIds.Contains(id))))
                    return false;

                return Key.Equals(y.Key);
            }

            public override int GetHashCode()
            {
                if (!StoresIds.Any())
                    return Key.GetHashCode();

                var storesIds = StoresIds.Select(id => id.ToString())
                    .Aggregate(string.Empty, (all, current) => all + current);

                return $"{storesIds}_{Key}".GetHashCode();
            }

            public override bool Equals(object obj)
            {
                var other = obj as CategoryKey;
                return other?.Equals(other) ?? false;
            }
        }

        public partial class ProductImportModel
        {
            public int ProductType { get; set; }
            public int ParentGroupedProductId { get; set; }
            public Boolean VisibleIndividually { get; set; }
            public int Vendor { get; set; }
            public int ProductTemplate { get; set; }
            public Boolean ShowOnHomepage { get; set; }
            public int DisplayOrder { get; set; }
            public string MetaKeywords { get; set; }
            public string MetaDescription { get; set; }
            public string MetaTitle { get; set; }
            public Boolean AllowCustomerReviews { get; set; }
            public string ManufacturerPartNumber { get; set; }
            public string Gtin { get; set; }
            public Boolean IsGiftCard { get; set; }
            public int GiftCardType { get; set; }
            public string OverriddenGiftCardAmount { get; set; }
            public Boolean RequireOtherProducts { get; set; }
            public string RequiredProductIds { get; set; }
            public Boolean AutomaticallyAddRequiredProducts { get; set; }
            public Boolean IsDownload { get; set; }
            public int DownloadId { get; set; }
            public Boolean UnlimitedDownloads { get; set; }
            public int MaxNumberOfDownloads { get; set; }
            public int DownloadActivationType { get; set; }
            public Boolean HasSampleDownload { get; set; }
            public int SampleDownloadId { get; set; }
            public Boolean HasUserAgreement { get; set; }
            public string UserAgreementText { get; set; }
            public Boolean IsRecurring { get; set; }
            public int RecurringCycleLength { get; set; }
            public int RecurringCyclePeriod { get; set; }
            public int RecurringTotalCycles { get; set; }
            public Boolean IsRental { get; set; }
            public int RentalPriceLength { get; set; }
            public int RentalPricePeriod { get; set; }
            public Boolean IsShipEnabled { get; set; }
            public Boolean IsFreeShipping { get; set; }
            public Boolean ShipSeparately { get; set; }
            public int AdditionalShippingCharge { get; set; }
            public int DeliveryDate { get; set; }
            public Boolean IsTaxExempt { get; set; }
            public int TaxCategory { get; set; }
            public Boolean IsTelecommunicationsOrBroadcastingOrElectronicServices { get; set; }
            public int ManageInventoryMethod { get; set; }
            public int ProductAvailabilityRange { get; set; }
            public Boolean UseMultipleWarehouses { get; set; }
            public int WarehouseId { get; set; }
            public int StockQuantity { get; set; }
            public Boolean DisplayStockAvailability { get; set; }
            public Boolean DisplayStockQuantity { get; set; }
            public int MinStockQuantity { get; set; }
            public int LowStockActivity { get; set; }
            public int NotifyAdminForQuantityBelow { get; set; }
            public int BackorderMode { get; set; }
            public Boolean AllowBackInStockSubscriptions { get; set; }
            public int OrderMinimumQuantity { get; set; }
            public int OrderMaximumQuantity { get; set; }
            public string AllowedQuantities { get; set; }
            public Boolean AllowAddingOnlyExistingAttributeCombinations { get; set; }
            public Boolean NotReturnable { get; set; }
            public Boolean DisableBuyButton { get; set; }
            public Boolean DisableWishlistButton { get; set; }
            public Boolean AvailableForPreOrder { get; set; }
            public string PreOrderAvailabilityStartDateTimeUtc { get; set; }
            public Boolean CallForPrice { get; set; }
            public int Price { get; set; }
            public int OldPrice { get; set; }
            public int ProductCost { get; set; }
            public Boolean CustomerEntersPrice { get; set; }
            public int MinimumCustomerEnteredPrice { get; set; }
            public int MaximumCustomerEnteredPrice { get; set; }
            public Boolean BasepriceEnabled { get; set; }
            public int BasepriceAmount { get; set; }
            public int BasepriceUnit { get; set; }
            public int BasepriceBaseAmount { get; set; }
            public int BasepriceBaseUnit { get; set; }
            public Boolean MarkAsNew { get; set; }
            public string MarkAsNewStartDateTimeUtc { get; set; }
            public string MarkAsNewEndDateTimeUtc { get; set; }
            public string Manufacturers { get; set; }
            public string ProductTags { get; set; }
            public string Name { get; set; }
            public string ShortDescription { get; set; }
            public string FullDescription { get; set; }
            public string SeName { get; set; }
            public Boolean Published { get; set; }
            public string SKU { get; set; }
            public string Categories { get; set; }
            public string Picture1 { get; set; }
            public string Picture2 { get; set; }
            public string Picture3 { get; set; }
            public decimal Weight { get; set; }
            public decimal Length { get; set; }
            public decimal Width { get; set; }
            public decimal Height { get; set; }
            public string RelatedProducts { get; set; }
            public Boolean IsLimitedToStores { get; set; }
        }

        #endregion
    }
}