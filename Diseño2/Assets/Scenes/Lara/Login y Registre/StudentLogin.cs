using UnityEngine;
using Firebase;
using Firebase.Auth;
using Firebase.Firestore;
using System.Collections.Generic;
using TMPro;
using System.Threading.Tasks;
using System.Collections;

public class StudentLogin : MonoBehaviour
{
    private FirebaseAuth auth;
    private FirebaseFirestore db;
    private bool isFirebaseInitialized;
    private FirebaseInitializer firebaseInitializer;
    private SceneLoader sceneLoader;

    [SerializeField] private TMP_InputField emailInput;
    [SerializeField] private TMP_InputField passwordInput;
    [SerializeField] private TMP_Text errorText;

    void Start()
    {
        firebaseInitializer = FindObjectOfType<FirebaseInitializer>();
        if (firebaseInitializer == null)
        {
            Debug.LogError("FirebaseInitializer not found in the scene.");
            return;
        }

        StartCoroutine(WaitForFirebaseInitialization());

        if (sceneLoader == null)
        {
            sceneLoader = FindObjectOfType<SceneLoader>();
            if (sceneLoader == null)
            {
                Debug.LogError("SceneLoader not found in the scene.");
            }
        }

        //if (!firebaseInitializer.IsInitialized)
        //{
        //    ShowError("Firebase no est� inicializado a�n. Por favor, espere.");
        //    return;
        //}

        auth = firebaseInitializer.Auth;
        db = firebaseInitializer.Db;
    }

    private IEnumerator WaitForFirebaseInitialization()
    {
        while (!firebaseInitializer.IsInitialized)
        {
            Debug.Log("Waiting for Firebase to initialize...");
            yield return new WaitForSeconds(0.5f);
        }

        auth = firebaseInitializer.Auth;
        db = firebaseInitializer.Db;
        isFirebaseInitialized = true;
        Debug.Log("Firebase Auth and Db references set in StudentLogin");

        if (auth == null)
        {
            Debug.LogError("Firebase Auth is still null after initialization in StudentLogin");
        }
        else
        {
            Debug.Log("Firebase Auth initialized successfully in StudentLogin");
        }
    }

    public void LoginStudent()
    {
        Debug.Log("LoginStudent method called");

        if (!isFirebaseInitialized || auth == null)
        {
            ShowError("Firebase no est� inicializado a�n. Por favor, espere.");
            return;
        }

        string email = emailInput.text.Trim();
        string password = passwordInput.text.Trim();

        Debug.Log($"Attempting to login user with email: {email}");

        if (!ValidateInputs(email, password))
        {
            return;
        }

        Debug.Log("Input validation passed, signing in user...");

        auth.SignInWithEmailAndPasswordAsync(email, password).ContinueWith(task => {
            if (task.IsCanceled)
            {
                ShowError("El inicio de sesi�n fue cancelado.");
                return;
            }
            if (task.IsFaulted)
            {
                HandleAuthError(task.Exception);
                return;
            }

            // Authentication successful
            FirebaseUser user = task.Result.User;
            Debug.Log($"User signed in successfully: {user.Email}");
            FetchStudentData(user.UserId);
            ShowSuccess("Inicio de sesi�n exitoso. �Bienvenido!");
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    private bool ValidateInputs(string email, string password)
    {
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            ShowError("El correo electr�nico y la contrase�a son obligatorios.");
            return false;
        }

        if (!System.Text.RegularExpressions.Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
        {
            ShowError("El formato del correo electr�nico no es v�lido.");
            return false;
        }

        if (password.Length < 6) // Agrega validaci�n de longitud de contrase�a
        {
            ShowError("La contrase�a debe tener al menos 6 caracteres.");
            return false;
        }

        return true;
    }

    private void HandleAuthError(System.AggregateException exception)
    {
        foreach (var innerException in exception.InnerExceptions)
        {
            Debug.LogError($"Authentication error: {innerException.Message}");
            if (innerException is FirebaseException firebaseEx)
            {
                var authError = (AuthError)firebaseEx.ErrorCode;
                Debug.LogError($"Firebase error code: {authError}");
                ShowError(GetFirebaseErrorMessage(authError));
            }
            else
            {
                Debug.LogError($"Unexpected error type: {innerException.GetType()}");
                ShowError("Se produjo un error inesperado durante el inicio de sesi�n.");
            }
        }
    }

    private async Task FetchStudentData(string userId)
    {
        try
        {
            Debug.Log($"Fetching student data for user: {userId}");

            DocumentReference userRef = db.Collection("Estudiantes").Document(userId);
            DocumentSnapshot snapshot = await userRef.GetSnapshotAsync();

            if (snapshot.Exists)
            {
                Dictionary<string, object> userData = snapshot.ToDictionary();
                if (userData.TryGetValue("Rol", out object rol) && rol.ToString() == "Estudiante")
                {
                    Debug.Log("Usuario verificado como estudiante");
                    OnSuccessfulLogin("Estudiante");
                }
                else
                {
                    Debug.LogWarning("El usuario no es un estudiante");
                    ShowError("Acceso denegado. Esta cuenta no pertenece a un estudiante.");
                }
            }
            else
            {
                ShowError("No se encontraron datos de usuario.");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error fetching user data: {ex.Message}");
            ShowError("Se produjo un error al obtener los datos del usuario.");
        }
    }

    private void ShowError(string message)
    {
        errorText.color = Color.red;
        errorText.text = message;
    }

    private void ShowSuccess(string message)
    {
        errorText.color = Color.green;
        errorText.text = message;
    }

    private void OnSuccessfulLogin(string role)
    {
        ShowSuccess("Inicio de sesi�n exitoso. �Bienvenido!");
        StartCoroutine(DelayedSceneChange(role));
    }

    private System.Collections.IEnumerator DelayedSceneChange(string role)
    {
        yield return new WaitForSeconds(1.5f);

        if (sceneLoader != null)
        {
            sceneLoader.LoadNextScene(role);
        }
        else
        {
            Debug.LogError("SceneLoader is not assigned. Cannot change scene.");
        }
    }

    private string GetFirebaseErrorMessage(Firebase.Auth.AuthError errorCode)
    {
        switch (errorCode)
        {
            case AuthError.MissingEmail:
                return "Por favor, ingrese un correo electr�nico.";
            case AuthError.InvalidEmail:
                return "El formato del correo electr�nico no es v�lido.";
            case AuthError.WrongPassword:
                return "La contrase�a es incorrecta. Por favor, int�ntelo de nuevo.";
            case AuthError.UserNotFound:
                return "No existe una cuenta asociada a este correo electr�nico. Por favor, verifique o reg�strese.";
            case AuthError.UserDisabled:
                return "Esta cuenta ha sido deshabilitada. Contacte al soporte para m�s informaci�n.";
            case AuthError.EmailAlreadyInUse:
                return "Este correo electr�nico ya est� en uso. �Olvid� su contrase�a?";
            case AuthError.WeakPassword:
                return "La contrase�a es demasiado d�bil. Debe tener al menos 6 caracteres.";
            case AuthError.OperationNotAllowed:
                return "Esta operaci�n no est� permitida. Contacte al soporte si cree que esto es un error.";
            default:
                return "Se produjo un error inesperado durante el inicio de sesi�n. Por favor, int�ntelo de nuevo.";
        }
    }
}
