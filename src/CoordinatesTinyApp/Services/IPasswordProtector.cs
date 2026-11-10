namespace CoordinatesTinyApp.Services;

internal interface IPasswordProtector
{
    string Protect(string password);

    string Unprotect(string encryptedPassword);
}
