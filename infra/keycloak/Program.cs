using Carpathians.Infra.Identity;

// Pulumi-програма: бажаний стан realm DevRealmDrivers. pulumi up порівнює його з Keycloak
// і створює / змінює / видаляє лише різницю (ідемпотентно).
return await Pulumi.Deployment.RunAsync<KeycloakStack>();
