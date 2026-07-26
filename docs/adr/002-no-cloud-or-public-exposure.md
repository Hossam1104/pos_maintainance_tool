# ADR 002: No cloud, central server, or public exposure

Status: DECIDED

## Decision

The product is a per-device local management tool. It has no cloud service, central server, public endpoint, or port-forwarding support.

## Justification

The operator works on one POS device. Central infrastructure and public reachability are outside the driver and would create security and operating cost disproportionate to this 4,198-line tool.
