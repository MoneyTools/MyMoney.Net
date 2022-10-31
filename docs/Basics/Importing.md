# Importing Data

There are multiple ways you can import data in to MyMoney.

The `File/Import` menu allows you to select the following file types

| type        | Description   |
| ------------- |-------------|
| .qif          | The Quicken interchange format (QIF). See [Online Banking](../Accounts/OnlineBanking.md)  |
| .ofx, .qfx    | Open Financial Exchange (OFX). See [Online Banking](../Accounts/OnlineBanking.md) |
| .xml          | An .xml file created from `Export Account...` on the Account context menu |
| .csv          | Comma separated file . See [CSV Import](../Accounts/CsvImport.md)    |
| .mmdb         | Import another MyMoney database |


If you are importing data from QFX files then the QFX file contains the account names, and import will automatically create the accounts for you.  **So QFX format is preferred over QIF or CSV.**   Some banks also provide OFX file download, which is just as good as QFX.  If you have to import QIF files then you will need to [create the accounts](../Accounts/SetupAccounts.md) first.