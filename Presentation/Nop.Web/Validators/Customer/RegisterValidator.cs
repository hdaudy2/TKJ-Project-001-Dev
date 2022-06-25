﻿using System;
using System.Linq;
using System.Text.RegularExpressions;
using FluentValidation;
using Nop.Core;
using Nop.Core.Domain.Customers;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Web.Framework.Validators;
using Nop.Web.Models.Customer;

namespace Nop.Web.Validators.Customer
{
    public partial class RegisterValidator : BaseNopValidator<RegisterModel>
    {
        public RegisterValidator(ILocalizationService localizationService,
            IStateProvinceService stateProvinceService,
            CustomerSettings customerSettings)
        {
            RuleFor(x => x.Email).NotEmpty().WithMessageAwait(localizationService.GetResourceAsync("Account.Fields.Email.Required"));
            RuleFor(x => x.Email).EmailAddress().WithMessageAwait(localizationService.GetResourceAsync("Common.WrongEmail"));

            if (customerSettings.EnteringEmailTwice)
            {
                RuleFor(x => x.ConfirmEmail).NotEmpty().WithMessageAwait(localizationService.GetResourceAsync("Account.Fields.ConfirmEmail.Required"));
                RuleFor(x => x.ConfirmEmail).EmailAddress().WithMessageAwait(localizationService.GetResourceAsync("Common.WrongEmail"));
                RuleFor(x => x.ConfirmEmail).Equal(x => x.Email).WithMessageAwait(localizationService.GetResourceAsync("Account.Fields.Email.EnteredEmailsDoNotMatch"));
            }

            if (customerSettings.UsernamesEnabled)
            {
                RuleFor(x => x.Username).NotEmpty().WithMessageAwait(localizationService.GetResourceAsync("Account.Fields.Username.Required"));
                RuleFor(x => x.Username).IsUsername(customerSettings).WithMessageAwait(localizationService.GetResourceAsync("Account.Fields.Username.NotValid"));
            }

            if (customerSettings.FirstNameEnabled && customerSettings.FirstNameRequired)
            {
                RuleFor(x => x.FirstName).NotEmpty().WithMessageAwait(localizationService.GetResourceAsync("Account.Fields.FirstName.Required"));
            }
            if (customerSettings.LastNameEnabled && customerSettings.LastNameRequired)
            {
                RuleFor(x => x.LastName).NotEmpty().WithMessageAwait(localizationService.GetResourceAsync("Account.Fields.LastName.Required"));
            }

            //Password rule
            RuleFor(x => x.Password).IsPassword(localizationService, customerSettings);

            RuleFor(x => x.ConfirmPassword).NotEmpty().WithMessageAwait(localizationService.GetResourceAsync("Account.Fields.ConfirmPassword.Required"));
            RuleFor(x => x.ConfirmPassword).Equal(x => x.Password).WithMessageAwait(localizationService.GetResourceAsync("Account.Fields.Password.EnteredPasswordsDoNotMatch"));

            //form fields
            if (customerSettings.CountryEnabled && customerSettings.CountryRequired)
            {
                RuleFor(x => x.CountryId)
                    .NotEqual(0)
                    .WithMessageAwait(localizationService.GetResourceAsync("Account.Fields.Country.Required"));
            }
            if (customerSettings.StateProvinceEnabled &&
                customerSettings.StateProvinceRequired)
            {
                RuleFor(x => x.StateProvinceId).MustAwait(async (x, context) =>
                {
                    //does selected country have states?
                    var hasStates = (await stateProvinceService.GetStateProvincesByCountryIdAsync(41)).Any();
                    if (hasStates)
                    {
                        //if yes, then ensure that a state is selected
                        if (x.StateProvinceId == 0)
                            return false;
                    }

                    return true;
                }).WithMessageAwait(localizationService.GetResourceAsync("Account.Fields.StateProvince.Required"));

                RuleFor(x => x.StateProvinceIdForShipping).MustAwait(async (x, context) =>
                {
                    //does selected country have states?
                    var hasStates = (await stateProvinceService.GetStateProvincesByCountryIdAsync(41)).Any();
                    if (hasStates)
                    {
                        //if yes, then ensure that a state is selected
                        if (x.StateProvinceIdForShipping == 0)
                            return false;
                    }

                    return true;
                }).WithMessageAwait(localizationService.GetResourceAsync("Account.Fields.StateProvince.Required"));
            }
            if (customerSettings.DateOfBirthEnabled && customerSettings.DateOfBirthRequired)
            {
                //entered?
                RuleFor(x => x.DateOfBirth).Must((x, context) =>
                {
                    if (!x.DateOfBirth.HasValue)
                        return false;

                    return true;
                }).WithMessageAwait(localizationService.GetResourceAsync("Account.Fields.DateOfBirth.Required"));
            }
            if (customerSettings.CompanyRequired && customerSettings.CompanyEnabled)
            {
                RuleFor(x => x.Company).NotEmpty().WithMessageAwait(localizationService.GetResourceAsync("Account.Fields.Company.Required"));
            }
            if (customerSettings.StreetAddressRequired && customerSettings.StreetAddressEnabled)
            {
                RuleFor(x => x.StreetAddress).NotEmpty().WithMessageAwait(localizationService.GetResourceAsync("Account.Fields.StreetAddress.Required"));
                RuleFor(x => x.StreetAddressForShipping).NotEmpty().WithMessageAwait(localizationService.GetResourceAsync("Account.Fields.StreetAddress.Required"));
            }
            if (customerSettings.StreetAddress2Required && customerSettings.StreetAddress2Enabled)
            {
                RuleFor(x => x.StreetAddress2).NotEmpty().WithMessageAwait(localizationService.GetResourceAsync("Account.Fields.StreetAddress2.Required"));
            }
            if (customerSettings.ZipPostalCodeRequired && customerSettings.ZipPostalCodeEnabled)
            {
                RuleFor(x => x.ZipPostalCode).NotEmpty().WithMessageAwait(localizationService.GetResourceAsync("Account.Fields.ZipPostalCode.Required"));
                RuleFor(x => x.ZipPostalCodeForShipping).NotEmpty().WithMessageAwait(localizationService.GetResourceAsync("Account.Fields.ZipPostalCode.Required"));
            }
            if (customerSettings.CountyRequired && customerSettings.CountyEnabled)
            {
                RuleFor(x => x.County).NotEmpty().WithMessageAwait(localizationService.GetResourceAsync("Account.Fields.County.Required"));
            }
            if (customerSettings.CityRequired && customerSettings.CityEnabled)
            {
                RuleFor(x => x.City).NotEmpty().WithMessageAwait(localizationService.GetResourceAsync("Account.Fields.City.Required"));
                RuleFor(x => x.CityForShipping).NotEmpty().WithMessageAwait(localizationService.GetResourceAsync("Account.Fields.City.Required"));
            }
            if (customerSettings.PhoneRequired && customerSettings.PhoneEnabled)
            {
                RuleFor(x => x.Phone).NotEmpty().WithMessageAwait(localizationService.GetResourceAsync("Account.Fields.Phone.Required"));
                RuleFor(x => x.PhoneForBilling).NotEmpty().WithMessageAwait(localizationService.GetResourceAsync("Account.Fields.Phone.Required"));
                RuleFor(x => x.PhoneForShipping).NotEmpty().WithMessageAwait(localizationService.GetResourceAsync("Account.Fields.Phone.Required"));
            }
            if (customerSettings.PhoneEnabled)
            {
                RuleFor(x => x.Phone).IsPhoneNumber(customerSettings).WithMessageAwait(localizationService.GetResourceAsync("Account.Fields.Phone.NotValid"));
                RuleFor(x => x.PhoneForBilling).IsPhoneNumber(customerSettings).WithMessageAwait(localizationService.GetResourceAsync("Account.Fields.Phone.NotValid"));
                RuleFor(x => x.PhoneForShipping).IsPhoneNumber(customerSettings).WithMessageAwait(localizationService.GetResourceAsync("Account.Fields.Phone.NotValid"));

                var regex = @"^\(?([0-9]{3})\)?[-.●]?([0-9]{3})[-.●]?([0-9]{4})$";
                
                RuleFor(x => x.Phone).Must((x, context) =>
                {
                    if(context != null){
                        var match = Regex.Match(context, regex, RegexOptions.IgnoreCase); 
                        if (!match.Success)
                            return false;
                    }

                    return true;
                }).WithMessageAwait(localizationService.GetResourceAsync("Account.Fields.Phone.NotValid"));
                
                RuleFor(x => x.PhoneForBilling).Must((x, context) =>
                {
                    if(context != null){
                        var match = Regex.Match(context, regex, RegexOptions.IgnoreCase); 
                        if (!match.Success)
                            return false;
                    }

                    return true;
                }).WithMessageAwait(localizationService.GetResourceAsync("Account.Fields.Phone.NotValid"));
                
                RuleFor(x => x.PhoneForShipping).Must((x, context) =>
                {
                    if(context != null){
                        var match = Regex.Match(context, regex, RegexOptions.IgnoreCase); 
                        if (!match.Success)
                            return false;
                    }

                    return true;
                }).WithMessageAwait(localizationService.GetResourceAsync("Account.Fields.Phone.NotValid"));

            }
            if (customerSettings.FaxRequired && customerSettings.FaxEnabled)
            {
                RuleFor(x => x.Fax).NotEmpty().WithMessageAwait(localizationService.GetResourceAsync("Account.Fields.Fax.Required"));
            }
            if (customerSettings.FaxEnabled)
            {
                RuleFor(x => x.Fax).Must((x, context) =>
                {
                    if(context != null){
                        var regex = @"^\(?([0-9]{3})\)?[-.●]?([0-9]{3})[-.●]?([0-9]{4})$";
                        var match = Regex.Match(context, regex, RegexOptions.IgnoreCase); 
                        if (!match.Success)
                            return false;
                    }

                    return true;
                }).WithMessageAwait(localizationService.GetResourceAsync("Account.Fields.Fax.NotValid"));
            }
        }
    }
}