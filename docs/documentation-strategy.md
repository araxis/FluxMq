# Documentation Strategy

FluxMQ documentation is split by audience.

## Developer Documentation

Developer documentation stays in `docs/`.

This includes:

- architecture notes
- component behavior
- flow lifecycle
- error model
- local development workflow
- material intended for future GitHub Wiki pages

Developer documentation should stay close to implementation changes and be reviewed with code.

## User Documentation

User documentation lives in `docs-site/`.

This is the GitHub Pages source for product-facing documentation.

This includes:

- getting started guides
- connection workflows
- recording and replay workflows
- Fork Flow usage
- user-facing reference material

The site is generated as static files, so it can be hosted by GitHub Pages without server-side runtime dependencies.

## Current Site Generator

The docs site uses VitePress.

Reasoning:

- Markdown-first authoring.
- Fast static builds.
- Local search support.
- Small maintenance surface.
- Good fit for GitHub Pages.

## Rule Of Thumb

If a document explains how FluxMQ is built, keep it in `docs/`.

If a document explains how to use FluxMQ, put it in `docs-site/`.
