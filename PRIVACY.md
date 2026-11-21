# Windows client data and privacy

The client is intentionally connected by a parent/operator to a Playtimely family. The hosted service and its source are separate from this MIT client. See the [service privacy policy](https://playtimely.com/privacy) for the hosted service's controller, retention, rights and contact details.

The service uses the configured HTTPS policy origin and device authentication secret to retrieve signed rules. It reports recognised-game daily usage totals (game key, family-local date, measured seconds), client/protocol information and Windows-account safety status. Fixed-shape time requests, parent-visible request state and checklist-completion changes are sent when used. HTTPS endpoints can also see ordinary network metadata such as IP address; signed update checks/downloads contact the configured Playtimely release service.

Process inspection takes place locally to recognise catalogued games, measure time and apply the parent's rules. This client is not a remote shell, screen recorder, key logger or browser-history collector. It does not upload arbitrary process command lines, window titles or screen contents as a monitoring feed. Inspect current source for exact message shapes.

The per-device connection secret and verified policy cache are restricted to SYSTEM/Administrators. The tray reads a separate secret-free local status projection; it does not receive the device secret. Usage/stop state can be persisted locally so offline rules and deadlines continue across restarts.

Install and connect only with the parent/operator's knowledge. Without connecting it to a family the client cannot provide the hosted-control function. Disconnect/uninstall to stop that function and its communication; this does not itself erase already-held server data or cancel a subscription. Use the service's account/privacy controls for those actions. Do not use personal-family configuration files as public examples or CI inputs.

Before any SignPath-signed installer release, verify that the installer visibly links this disclosure/service privacy policy and that connection/usage communication is an explicit parent/operator choice. That release gate is not claimed as already approved by SignPath.
