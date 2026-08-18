# Jumoo.TranslationManager.Automate

Translation Manager actions and triggers for [Umbraco Automate](https://jumoo.co.uk/) — create,
submit and approve Translation Manager jobs from Automate workflows, and start workflows from
translation events.

The classic use case: **when content is published, translate it**. Wire
`Content Published` → `Translate Content` → `Check Translation Job` → `Approve Translation Job`
into an automation, no code required.

## Actions

### Translate Content — `translationManager.translateContent`

Creates translation nodes for a content item across its matching translation sets and submits
one job per target culture.

| Setting | Type | Notes |
|---|---|---|
| Content | string (GUID) | Bindable. `${trigger.contentKey}` picks up the item that started the automation |
| Target Cultures | CSV | Bindable. Blank = every culture in the matching sets |
| Translation Sets | CSV of set keys | Blank = every set covering the item |
| Translation Connector | GUID | Blank = the set's own connector, then the configured default |
| Job Name | string | Bindable. Blank = `"{content} ({count}) to {culture}"` |
| Include Children | bool | Also translate every descendant |
| Force Retranslate | bool | Creates nodes even where nothing has changed since the last translation |
| Approve when returned | bool | Approves synchronously as soon as the connector returns - only use with instant connectors |
| Publish approved content | bool | Only applies with "Approve when returned" |

Output: `jobIds`, `jobKeys`, `cultures`, `setNames`, `providerKey`, `providerName`, `groupId`,
`jobs` (capped array of job summaries), `nodeCount`, `jobCount`.

```
${translate.jobKeys}          → feed into umbracoAutomate.forEach
${translate.jobCount}          → branch on how many jobs were created
```

Content that isn't covered by any translation set is a **Skipped** step, not a failure - this
matters when the action sits behind a broad `Content Published` trigger.

### Check Translation Job — `translationManager.checkJob`

Polls a translation connector for the latest status of a job. Returns
`SuccessWithOutcome(status, output)`, so a workflow can branch directly on the outcome without a
separate `If` step - this is what makes a "translate → sleep → check → approve" polling loop
buildable out of the box.

| Setting | Type | Notes |
|---|---|---|
| Translation Job | string (GUID) | Bindable. `${translate.jobKeys[0]}` or a trigger's job key |
| Ask the connector | bool | Off reads Translation Manager's stored status only |

### Approve Translation Job — `translationManager.approveJob`

Approves a translation job and optionally publishes the translated content.

| Setting | Type | Notes |
|---|---|---|
| Translation Job | string (GUID) | Bindable |
| Ask the connector first | bool | Refreshes the translation before approving |
| Publish approved content | bool | |
| Approve all nodes | bool | Off restricts approval to the node keys below |
| Node Keys | CSV | Bindable. Only used when "Approve all nodes" is off |

Before approving, the service account is checked against every node's master content via
`IAutomationActionAuthorizer.FilterAuthorizedContentAsync` - the whole job is refused rather than
partially approved if any content is outside the account's authorized set.

## Triggers

- **Translation Job Submitted** (`translationManager.jobSubmitted`)
- **Translation Received** (`translationManager.jobReceived`) - the important one: work came back
- **Translation Job Approved** (`translationManager.jobApproved`)
- **Translation Published** (`translationManager.jobPublished`)

Only past-tense Translation Manager events are exposed as triggers. Translation Manager's
`-ing`/cancelable notifications (creating, submitting, approving, publishing) are not - Automate
dispatches triggers asynchronously, so a trigger cannot cancel the operation that raised it, and
exposing them would be a UI lie.

Each trigger shares the same settings: filter by translation set, target culture, connector, and
whether to ignore jobs that came back in an error state. Output includes `jobKey`, `jobId`,
`targetCulture`, `status`, `contentKeys` (capped array of master content keys - feed into
`umbracoAutomate.forEach` → `umbracoAutomate.publishContent`), and `setKey`.

## Loop protection

Approving or publishing a translation writes content, which can re-trigger a `Content Published`
automation that itself calls `Translate Content`. This package guards against that in three ways:

1. **`JobOptions.AutoApprove` and `TranslationJobApprovalOptions.ApproveInBackground` are always
   `false`.** Translation Manager's own background auto-approver runs outside Umbraco Automate's
   ambient automation origin, which defeats Automate's `SkipOnCycle` loop guard on any
   content-published trigger chained after this action. Approval, when requested, always happens
   synchronously on the same thread as the triggering step.
2. Every trigger defaults `OnAutomationOriginatedEvent` to **Skip if this would loop**.
3. An in-process gate suppresses this package's own triggers while one of its actions is actively
   writing a translation, configurable via `TranslationManager:Automate:SuppressSelfOriginatedEvents`
   (default on).

If you rely on Translation Manager's own background auto-approve for a set instead of this
package's actions, be aware that content published that way is **not tagged with an automation
origin** - filter your content triggers by content type or path instead of relying on the loop
guard.

## Configuration

```json
{
  "TranslationManager": {
    "Automate": {
      "DefaultProviderKey": null,
      "MaxWaitSeconds": 0,
      "MaxContentKeysPerEvent": 100,
      "SuppressSelfOriginatedEvents": true,
      "AllowSuperUserFallback": false,
      "ExcludedSets": []
    }
  }
}
```

## License

[MPL-2.0](../LICENSE)
