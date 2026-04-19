package com.nexus.sdk;

/** Thrown for non-2xx HTTP responses. */
public class NexusException extends Exception {
    private final int statusCode;

    public NexusException(int statusCode, String message) {
        super("HTTP " + statusCode + ": " + message);
        this.statusCode = statusCode;
    }

    public int getStatusCode() { return statusCode; }
}
