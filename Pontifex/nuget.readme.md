# Pontifex

A C# networking library providing reliable and unreliable message delivery patterns with acknowledgment support.

## Overview

Pontifex is a transport abstraction layer designed to simplify network communication in C# applications. It provides multiple delivery guarantees and communication patterns through a pluggable architecture.

## Key Features

- **Multiple Delivery Patterns**: Support for acknowledged and unacknowledged message delivery
- **Request-Reply Pattern**: Built-in support for request-response communication
- **Transport Abstraction**: Pluggable transport implementations via factory pattern
- **Synchronized Handlers**: Thread-safe message handling utilities
- **Delivery ID Management**: Cyclic delivery ID tracking with wrap-around semantics
- **Endpoint Flexibility**: Support for various endpoint types (GUID, Long, String, Typed, Void)
- **Logging & Monitoring**: Integrated logging wrappers for AckRaw transports
- **Traffic Control**: Built-in mechanisms for delivery control and traffic management

## Architecture

The library is organized into several key components:

- **Abstractions**: Core interfaces for controls, endpoints, and transports
- **Transports**: Transport implementations with acknowledgment support
- **Handlers**: Message processing and synchronization utilities
- **Factory**: Transport registration and instantiation
- **Utils**: Helper utilities including FSM, action queues, and traffic controllers
- **Wrappers**: Logging and monitoring capabilities

## Requirements

- .NET Framework / .NET Core compatible C# project