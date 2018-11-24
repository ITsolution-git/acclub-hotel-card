# Axion WebSocket Server

*Axion Comm. Web Socket server for HUD.*

## What Is It?

JavaScript written on the Node.js engine that handles real-time communication for the HUD.


## How To Install

*Make sure you have Node 8.x or more installed*

`npm i`

## Setup

`cp .env_example .env`

After creating .env, open it with your favorite editor and fill out the variables.

## How To Run

`node ./serve`

## Current Features

Check if juan@juan.com call status is online/active, idle, busy. -- We plan on expanding this for more users. ;)

## Roadmap

- Add client factory for new connections to filter wanted params.
- Add unit testing framework.
- Add support for custom status messages.
- Clean up some code sections for readability.
- Add Redis support.
- Add RabbitMQ support.
