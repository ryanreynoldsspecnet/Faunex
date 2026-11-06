# Admin MFA pilot

Platform roles and tenant administrators can opt in under **Security**. Buyers and sellers are unchanged. Do not require enrollment across the platform until the recovery exercise below is complete and the owner approves enforcement.

1. Sign in with your current password. Open Security, enter the password again, and scan the authenticator QR code or enter its key. Confirm with the current six-digit code.
2. Save the eight one-use recovery codes separately from the device (for example, in an approved password manager). They are shown only once. Sign in again; wait for the app to show a new code if the enrollment code is still displayed.
3. For a lost device, enter your password and choose **Use a recovery code**. Recovery revokes previous sessions and sends the admin to Security. Set up the new device, save the replacement codes, and sign in again. The old authenticator, old recovery codes, and all existing sessions stop working when replacement succeeds.

An email password reset does not remove MFA. There is no email-only or support override for someone who has lost both the device and all recovery codes. Escalate to the responsible platform or tenant owner through an independently verified channel. An already-authorized owner can provision a separate replacement account under the normal access approval process, then disable the inaccessible account. Do not share accounts, edit MFA database fields, or send secrets over email.

Before mandatory rollout, exercise enrollment and replacement for a test platform admin and a test tenant admin on their proper origins. Verify an old session fails, an old code cannot be replayed, and an email password reset still requires the second factor. Keep more than one authorized administrator with tested recovery; never start enforcement with only one recoverable operator.

MFA authenticator keys are encrypted with the API's existing ASP.NET Core Data Protection key ring. Back up and restore the identity database and that key ring together using the protected release recovery runbook. Recovery codes are stored as hashes. Keep a compatible MFA-aware build for rollback once an account enrolls; an older build does not enforce these MFA columns.
