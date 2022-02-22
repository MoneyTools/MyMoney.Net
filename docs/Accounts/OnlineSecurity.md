# Online Security

All communication with your bank over the OFX protocol is protected by encryption using the **Secure Sockets Layer**  (SSL) over the standard web HTTPS protocol, which is the same level of protection you get when you logon to your bank using your web browser.  The connection to the bank is a direct connection with no other servers in between (unlink [Mint](https://www.mint.com/terms) which uses one or more third party online services to gather your account information in the cloud).

SSL encryption is hard to break, even if someone was spying on your network watching everything going over the wire, this is what they would see in a typical conversation between MyMoney and your bank.

![](../Images/Online%20Security.png)

Decrypting this requires keys that a random spy will not have, which makes your data secure.

Besides SSL and the normal account userid and password, OFX also defines additional protocols for additional credentials.  These are called Multi-Factor Authentication, AuthToken and ChangePassword .  All these add another layer of security which helps your bank be even more careful about granting online access to your bank accounts.

**Additional Credentials**
When you first connect to your bank using the Download Accounts dialog your bank may send some additional fields that need to be filled in order to authenticate your connection.  These fields will show up on the Password Dialog, as shown below:

![](../Images/Online%20Security1.png)


**Multi-Factor Authentication**
Your bank can also ask for additional information at any time in the future, just as a double check using the MFAChallenge protocol.  If this happens you will see the following dialog, with different questions depending on your bank.

![](../Images/Online%20Security2.png)

**AuthToken**
Similarly, your bank may request an Authentication Token from you, providing information to you on how to get one.  This may involve visiting their web site or making a phone call.  Once you have the token you can enter it here and then your bank will grant you online access to your bank accounts.

![](../Images/Online%20Security3.png)

**Change Password**  
Your bank may also request a password change.  If this happens you will see this dialog:

![](../Images/Online%20Security4.png)
