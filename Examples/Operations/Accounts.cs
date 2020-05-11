﻿using System.Collections.Generic;
using System.Net.Http;
using UngerboeckSDKWrapper;
using UngerboeckSDKPackage;
using System;

namespace Examples.Operations
{
  public class Accounts : Base
  {
    public Accounts(HttpClient USISDKClient) : base(USISDKClient) { }

    /// <summary>
    /// A basic retrieve example
    /// </summary>   
    public AllAccountsModel Get(string orgCode, string accountCode)
    {
      return APIUtil.GetAccount(USISDKClient, orgCode, accountCode);
    }

    /// <summary>
    /// A basic edit example
    /// </summary>  
    public AllAccountsModel Edit(string orgCode, string accountCode, string newCityValue)
    {
      var myAccount = APIUtil.GetAccount(USISDKClient, orgCode, accountCode);

      myAccount.City = newCityValue;

      return APIUtil.UpdateAccount(USISDKClient, myAccount);
    }

    public AllAccountsModel AddOrganizationAccount(string OrgCode, string newAccountName)
    {
      var myAccount = new AllAccountsModel
      {
        Organization = OrgCode,
        Name = newAccountName,
        EventSalesStatus = "A", //Use your configured Status codes to set the account designation status property
        Class = USISDKConstants.AccountClass.Account //The class determines if this is an Account or a Contact

        //myAccount.AccountCode = "ACCTCODE" 'You can set the Account code manually, or leave it empty if your configuration allows for it to be set automatically.
      };

      return APIUtil.AddAccount(USISDKClient, myAccount);
    }

    public AllAccountsModel AddContact(string orgCode, string newAccountFirstName, string newAccountLastName)
    {
      var myAccount = new AllAccountsModel
      {
        Organization = orgCode,
        FirstName = newAccountFirstName,
        LastName = newAccountLastName,
        EventSalesStatus = "A", //Use your configured Status codes to set the account designation status property
        Class = USISDKConstants.AccountClass.Contact //The class determines if this is an Account or a Contact

        //myAccount.AccountCode = "ACCTCODE" 'You can set the Account code manually, or leave it empty if your configuration allows for it to be set automatically.
      };

      return APIUtil.AddAccount(USISDKClient, myAccount);
    }

    /// <summary>
    /// If you wish to have duplicate checking for newly added accounts, this is how you would do it
    /// </summary>  
    public AllAccountsModel AddAccountWithDuplicateChecking(string orgCode, string firstName, string lastName, string email)
    {
      var myAccount = new AllAccountsModel
      {
        Organization = orgCode,
        FirstName = firstName,
        LastName = lastName,
        Email = email,
        Class = USISDKConstants.AccountClass.Contact
      };

      return APIUtil.AddAccountWithDuplicateCheck(USISDKClient, myAccount);
    }

    public IEnumerable<AllAccountsModel> RetrieveAll(string orgCode)
    {
      SearchMetadataModel searchMetadata = null;
      return APIUtil.GetSearchList<AllAccountsModel>(USISDKClient, ref searchMetadata, orgCode, "All");
    }

    /// <summary>
    /// Examples showing how to search using UserFields
    /// </summary>
    /// <param name="orgCode">Organization Code where the search will take place.  User fields are organization-based.</param>
    /// <returns></returns>
    public IEnumerable<AllAccountsModel> SearchForUserFields(string orgCode)
    {
      SearchMetadataModel searchMetadata = null;

      List<AllAccountsModel> accounts = new List<AllAccountsModel>();

      //For account user fields, the format is [Account User Field Header flag (O ([organizaion], P [individual], M [membership])]|[User field Class]|[User field Type]|[Organization Code]|[User field property name]"
      //This will only work for active User Fields in your organization.
      //Note for multi-value UDFs, it will convert to a CONTAINS search.

      //This is searching for Individual Account User fields of Issue Class = C (event sales), Issue Type code = 85, organization code = 10, and User Number 01 (AMT_01).  It will return accounts where the value is 12
      accounts.AddRange(APIUtil.GetSearchList<AllAccountsModel>(USISDKClient, ref searchMetadata, orgCode, "P|C|CK|10|UserNumber01 eq 11"));

      //You can mix it in with other filter conditions as well.  This is searching for Postal Code = '77777' with an Organization User Field set
      accounts.AddRange(APIUtil.GetSearchList<AllAccountsModel>(USISDKClient, ref searchMetadata, orgCode, "PostalCode eq '77777' and O|C|04|10|UserText01 eq '01'"));

      //You can search for date user fields.  This is looking for a Registration class User field date of type '3T'
      accounts.AddRange(APIUtil.GetSearchList<AllAccountsModel>(USISDKClient, ref searchMetadata, orgCode, "P|R|3T|10|UserDateTime01 eq datetime'1979-10-27'"));

      ////You can search for membership user fields stored on accounts.
      //accounts.AddRange(APIUtil.GetSearchList<AllAccountsModel>(USISDKClient, ref searchMetadata, orgCode, "M|M|01|10|UserText04 eq 'CHECK'"));

      //This also works for searching for null values or non-null values
      accounts.AddRange(APIUtil.GetSearchList<AllAccountsModel>(USISDKClient, ref searchMetadata, orgCode, "P|C|CK|10|UserNumber01 ne null"));

      return accounts;
    }

    /// <summary>
    /// You can return User Fields while searching by requesting the fields on custom objects
    /// </summary>
    /// <param name="orgCode">Organization Code where the search will take place.  User fields are organization-based.</param>
    /// <returns>Accounts with a user field</returns>
    public IEnumerable<AllAccountsModel> ReturningUserFieldsDuringSearch(string orgCode)
    {
      SearchMetadataModel searchMetadata = null;

      //By using the $Select ability to make a custom return object, you can retrieve user fields on searching, with
      //minimal performance cost.

      //For account user fields, the format is [User field Class]|[User field Type]|[User field property name]
      //This will only work for active User Fields in your organization.

      //This will return Account User fields of Issue Class = C (event sales), Issue Type code = 04, and User Number 01 (AMT_01).  It will return accounts where the Account Rep code is 0002410
      List<string> returnedFields = new List<string> { "C|04|UserText01" , "LastName" };

      IEnumerable<AllAccountsModel> accounts = APIUtil.GetSearchList<AllAccountsModel>(USISDKClient, ref searchMetadata, orgCode, "AccountRep eq '0002410'","", 1000, 100000, returnedFields);

      return accounts;
    }

    /// <summary>
    /// This example will show you how to add a new account with custom fields attached.
    /// </summary>
    /// <param name="orgCode">The organization code of the account you wish to add.</param>
    /// <param name="newAccountName">This is the the name of the new account.</param>
    /// <param name="issueClass">This is the code that represents the Issue Class of the custom fields.</param>
    /// <param name="issueType">This is the code that represents the Issue Type of the custom fields</param>
    /// <param name="newTxt01Value">In this example, we are changing Text_01.  Enter a new value here to set this.</param>    
    public AllAccountsModel AddWithUserFields(string orgCode, string newAccountName, string issueClass, string issueType, string newTxt01Value)
    {
      var myAccount = new AllAccountsModel
      {
        Organization = orgCode,
        Name = newAccountName,
        EventSalesStatus = "A", //Use your configured Status codes to set the account designation status property
        Class = USISDKConstants.AccountClass.Account, //The class determines if this is an Account or a Contact
        AccountUserFieldSets = new List<UserFields>()
      };

      //Here's how to add a user field set with values to a new account
      var myUserField = new UserFields
      {
        Header = USISDKConstants.UserFieldHeaders.OrganizationAccountUserFields,  //Designate if this is an organization account user field set, an individual account user field set, or a membership user field set
        Class = issueClass, //Set the designation of the user field.  You can use the USISDKConstants.AccountDesignations class to set this.
        Type = issueType, //Use the Opportunity Type code from your user field.  This matches the value stored in Ungerboeck table column CR073_ISSUE_TYPE.
        UserText01 = newTxt01Value //Set the value in the user field property
      };

      myAccount.AccountUserFieldSets.Add(myUserField); //Then add it back into the AllAccountsModel object.  You can add multiple user field sets to the same account object before saving.

      return APIUtil.AddAccount(USISDKClient, myAccount);
    }

    /// <summary>
    /// This shows various ways you can edit Account user fields
    /// </summary>
    public AllAccountsModel EditWithUserFields(string orgCode, string accountCode)
    {
      var myAccount = APIUtil.GetAccount(USISDKClient, orgCode, accountCode);

      //To change userfields, search through the AccountUserFieldSets object on the account model.
      //Find the user field set that matches the designation and the Opportunity Type code.
      //Once it is found, you can change whatever user field you wish.
      if (myAccount.AccountUserFieldSets != null)
      {
        foreach (UserFields objAccountUserFields in myAccount.AccountUserFieldSets)
        {
          if (objAccountUserFields.Header == UngerboeckSDKPackage.USISDKConstants.UserFieldHeaders.OrganizationAccountUserFields ||
                objAccountUserFields.Header == UngerboeckSDKPackage.USISDKConstants.UserFieldHeaders.IndividualAccountUserFields)
          {
            //These are User fields that are set under the configuration window for each account designation
            //The classes here are just examples.  All designations can be used.
            if (objAccountUserFields.Class == UngerboeckSDKPackage.USISDKConstants.AccountDesignations.EventSales &&
                objAccountUserFields.Type == "CK")
            {
              //In this case, this is checking for a User Field set of code "CK" that is configured under the Event Sales Configuration window as either
              //an Individual default user field or an Organization default user field
              objAccountUserFields.UserNumber02 = 7777;  //Set the value for the user number 02 field (AMT_02)
            }
            else if (objAccountUserFields.Class == UngerboeckSDKPackage.USISDKConstants.AccountDesignations.Registration &&
                  objAccountUserFields.Type == "04")
            {
              objAccountUserFields.UserDateTime01 = System.DateTime.Now.AddDays(1);
              objAccountUserFields.UserDateTime02 = System.DateTime.Now.AddHours(3);
              objAccountUserFields.UserText06 = "2332,2333"; //This is a multi-value user field.  For more than one selected value, insert commas between multiple codes.
            }
            else if (objAccountUserFields.Class == UngerboeckSDKPackage.USISDKConstants.AccountDesignations.PublicRelations &&
                  objAccountUserFields.Type == "PT")
            {
              objAccountUserFields.UserText03 = "SDKValue";
            }
          }
        }
      }

      return APIUtil.UpdateAccount(USISDKClient, myAccount);

    }

    /// <summary>
    /// Example of how to update membership fields on accounts
    /// </summary>
    public AllAccountsModel EditMembershipFields(string orgCode, string accountCode, int newMemberRank)
    {
      var myAccount = APIUtil.GetAccount(USISDKClient, orgCode, accountCode);

      if (myAccount.Membership == "A")
      {
        //Based on active designation flags, you can determine if this account uses the membership fields.
        //The 'A' here represents your code for the Active designation
        myAccount.MemberCategoryDate = DateTime.Now;
        myAccount.MemberRank = newMemberRank;

        //Below are some other fields that can be set via the API
        //myAccount.MemberCategoryDate = Now
        //myAccount.MemberTypeDate = Now
        //myAccount.MemberStatusDate = Now
        //myAccount.MemberSince = Now
        //myAccount.MemberStatus = "A"
        //myAccount.MemberCategory = "WD"
        //myAccount.MemberType = "1YEAR"
        //myAccount.MemberReason = "RA"            

        return APIUtil.UpdateAccount(USISDKClient, myAccount);
      }
      else
      {
        throw new Exception("Membership accounts only");
      }
    }

    /// <summary>
    /// This shows various ways you can edit Account user fields
    /// </summary>
    /// <param name="issueType">This is the Issue Type code of the membership user field set.  Example string value "CK"</param>
    /// <param name="newUserText05Value">As an example, we are setting User Text 05.  Fill this with a string.</param>
    public AllAccountsModel EditWithMembershipUserFields(string orgCode, string accountCode, string issueType, string newUserText05Value)
    {
      var myAccount = APIUtil.GetAccount(USISDKClient, orgCode, accountCode);

      //To change userfields, search through the AccountUserFieldSets object on the account model.
      //Find the user field set that matches the designation and the Opportunity Type code.
      //Once it is found, you can change whatever user field you wish.
      if (myAccount.AccountUserFieldSets != null)
      {
        foreach (UserFields objAccountUserFields in myAccount.AccountUserFieldSets)
        {
          if (objAccountUserFields.Header == UngerboeckSDKPackage.USISDKConstants.UserFieldHeaders.MembershipUserFields)
            //These are the Membership specific User fields that is configued for all accounts with a membership designation.
            if (objAccountUserFields.Type == issueType)
              objAccountUserFields.UserText05 = newUserText05Value;        
        }
      }

      return APIUtil.UpdateAccount(USISDKClient, myAccount);

    }
  }

        

}

