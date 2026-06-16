import groovy.json.JsonSlurper
import java.util.Base64

plugins {
    id("com.android.application")
    id("kotlin-android")
    // The Flutter Gradle Plugin must be applied after the Android and Kotlin Gradle plugins.
    id("dev.flutter.flutter-gradle-plugin")
}

val dartDefines = if (project.hasProperty("dart-defines")) {
    project.property("dart-defines")
        .toString()
        .split(",")
        .associate { encoded ->
            val decoded = String(Base64.getDecoder().decode(encoded))
            val separator = decoded.indexOf('=')
            decoded.substring(0, separator) to decoded.substring(separator + 1)
        }
} else {
    emptyMap()
}

val localApiKeysFile = rootProject.file("../env/api_keys.local.json")
val localApiKeys = if (localApiKeysFile.exists()) {
    @Suppress("UNCHECKED_CAST")
    JsonSlurper().parse(localApiKeysFile) as Map<String, Any?>
} else {
    emptyMap()
}

fun apiKey(name: String): String {
    return dartDefines[name]
        ?.takeIf { it.isNotBlank() }
        ?: localApiKeys[name]?.toString()?.takeIf { it.isNotBlank() }
        ?: ""
}

android {
    namespace = "com.android.safe_ride"
    compileSdk = flutter.compileSdkVersion
    ndkVersion = flutter.ndkVersion

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }

    kotlinOptions {
        jvmTarget = JavaVersion.VERSION_17.toString()
    }

    defaultConfig {
        // TODO: Specify your own unique Application ID (https://developer.android.com/studio/build/application-id.html).
        applicationId = "com.android.safe_ride"
        // You can update the following values to match your application needs.
        // For more information, see: https://flutter.dev/to/review-gradle-config.
        minSdk = flutter.minSdkVersion
        targetSdk = flutter.targetSdkVersion
        versionCode = flutter.versionCode
        versionName = flutter.versionName
        manifestPlaceholders["googleMapsApiKey"] = apiKey("GOOGLE_MAPS_API_KEY")
        resValue(
            "string",
            "default_web_client_id",
            apiKey("GOOGLE_SERVER_CLIENT_ID"),
        )
    }

    buildTypes {
        release {
            // TODO: Add your own signing config for the release build.
            // Signing with the debug keys for now, so `flutter run --release` works.
            signingConfig = signingConfigs.getByName("debug")
        }
    }
}

flutter {
    source = "../.."
}
