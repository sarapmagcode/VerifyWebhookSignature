# hook0 (self-hosted)

By Mark Jason T. Galang ([@markjasongalang](https://github.com/markjasongalang))

## Getting Started with Hook0

### Set Up Environment Variables

```bash
# Set your service token (from dashboard)
export HOOK0_TOKEN="YOUR_TOKEN_HERE"
export HOOK0_API="https://app.hook0.com/api/v1" # Replace by your domain (or http://localhost:8081 locally)

# Set your application ID (shown in dashboard URL or application details)
export APP_ID="YOUR_APPLICATION_ID_HERE"
```

Regarding the commands above, if you're using [PowerShell](https://en.wikipedia.org/wiki/PowerShell), then you can do the equivalent below:

```powershell
$env:HOOK0_TOKEN="YOUR_TOKEN_HERE"
$env:HOOK0_API="http://localhost:8081/api/v1"
$env:APP_ID="YOUR_APPLICATION_ID_HERE"
```

Refer to [What is the PowerShell equivalent of the Unix shell `export` command?](https://stackoverflow.com/questions/62308709/what-is-the-powershell-equivalent-of-the-unix-shell-export-command)

To display the value set:

```powershell
$env:HOOK0_TOKEN
```

**CAUTION**

This is only for the current powershell session. It **DOESN'T** persist after the session closes.

Refer to [PowerShell Environment Variables: Get, Set, and Use Them](https://www.powershelltips.com/powershell-environment-variables/)

### Step 1: Start Hook0 (Self-Hosted Only)

```powershell
docker-compose up -d
```

#### Issue #1

When I ran this command on my Windows 11 pc, I encountered this error below on `hook0/api`, `hook0/output-worker`, and `hook0/frontend`:

```
/bin/sh: 1: set: Illegal option -
```

<u>Solution</u>

Simply modify the *Dockerfile* of each container above, specifically, update the *End of Line Sequence* to **LF** instead of CRLF.

Refer to [Docker RUN: failed to resolve process when trying to build a Dockerfile](https://stackoverflow.com/questions/78230180/docker-run-failed-to-resolve-process-when-trying-to-build-a-dockerfile)

<u>Debugging</u>

More importantly, what helped me tremendously on debugging is the **Builds** tab on Docker Desktop because each container being built is displayed here and you can check the logs to help resolve the issue.

#### Issue #2

But, I encountered another problem on `hook0/frontend`:

```
(...truncated for brevity)

transforming...

/app/src/App.vue
1:25  error  Delete `␍`  prettier-vue/prettier
2:51  error  Delete `␍`  prettier-vue/prettier
3:39  error  Delete `␍`  prettier-vue/prettier
4:36  error  Delete `␍`  prettier-vue/prettier
5:38  error  Delete `␍`  prettier-vue/prettier
6:31  error  Delete `␍`  prettier-vue/prettier
7:46  error  Delete `␍`  prettier-vue/prettier
8:52  error  Delete `␍`  prettier-vue/prettier
9:42  error  Delete `␍`  prettier-vue/prettier
10:75  error  Delete `␍`  prettier-vue/prettie

(...truncated for brevity)
```

<u>Solution (temporary)</u>

After trying so many things that I tried, what worked is by simple commenting the prettier stuff in `.eslintrc.cjs`:

```javascript
// ...truncated for brevity

  extends: [
    'plugin:vue/vue3-essential',
    'plugin:vue/vue3-recommended',
    'plugin:@typescript-eslint/recommended',
    'plugin:@typescript-eslint/recommended-requiring-type-checking',
    // 'plugin:prettier-vue/recommended', <-- This!!
    // 'prettier', <-- This also!!
  ],

// ...truncated for brevity
```

Refer to [How to Fix Delete `··` (prettier/prettier) Errors in a Vue JS Project: Step-by-Step Guide](https://www.xjavascript.com/blog/how-to-get-rid-of-delete-prettier-prettier-errors-in-a-vue-js-project/)

Afterwards, the build continued and started the other containers except for `hook0/frontend` (again) which led to the issue below.

### Issue #3

`hook0/frontend`

```
env: can't execute 'sh

': No such file or directory

env: can't execute 'sh

': No such file or directory

```

> Note: I found these logs on Docker Desktop > Containers > hook0-frontend-1 > Logs

<u>Solution</u>

After so many tries, I finally got it working by doing the steps below.

Make sure there's no `hook0-frontend:latest` image:

```powershell
docker image rm hook0-frontend:latest
```

> Note: This is very important because if the error still persists and you try to modify the *docker-entrypoint.sh* file, the changes will **NOT** reflect because the image has already been created.

Then, simply change the *End of Line Sequence* of the `/frontend/scripts/docker-entrypoint.sh` (file) to **LF** instead of CRLF.

Afterwards, start the containers:

```powershell
docker compose up -d
```

Refer to [How to find out line-endings in a text file?](https://stackoverflow.com/questions/3569997/how-to-find-out-line-endings-in-a-text-file)

Output:

```
...truncated for brevity

#16 resolving provenance for metadata file
#16 DONE 0.0s
[+] up 7/7
 ✔ Image hook0-frontend            Built                                                                          322.8s
 ✔ Network hook0_hook0             Created                                                                          0.1s
 ✔ Container hook0-postgres-1      Healthy                                                                          7.1s
 ✔ Container hook0-mailpit-1       Healthy                                                                          2.8s
 ✔ Container hook0-api-1           Healthy                                                                         11.9s
 ✔ Container hook0-output-worker-1 Started                                                                         12.1s
 ✔ Container hook0-frontend-1      Started                                                                         12.1s
```

### Try it out

Go to <http://localhost:8001/> and create an account.

> Note: To verify the email, you'll need to go at Mailpit: <http://localhost:8025/>.

Finally, you can now use hook0 locally :))

### Create a Webhook Subscription (step 5 on the guide)

```powershell
$headers = @{
  "Authorization" = "Bearer $env:HOOK0_TOKEN"
  "Content-Type" = "application/json"
}

$body = @{
  application_id = "$env:APP_ID"
  is_enabled = ${true}
  event_types = @("user.account.created")
  description = "Tutorial webhook endpoint"
  labels = @{ "environment" = "tutorial" }
  target = @{
    "type" = "http"
    "method" = "POST"
    "url" = "https://play.hook0.com/in/c_80O0yKdQNrJ8v6ENHjzZhLvK8ks/"
    "headers" = @{ "X-Custom-Header" = "my-value" }
  }
} | ConvertTo-Json

$response = Invoke-WebRequest -Uri "$env:HOOK0_API/subscriptions" -Headers $headers -Method POST -Body $body

$response.Content | ConvertFrom-Json
```

> Note: Don't forget to set the environment variables `$env:HOOK0_TOKEN`, `$env:HOOK0_API`, `$env:APP_ID` .

Output:

```powershell
application_id    : 4698d326-e61f-44f1-bf3d-1518c8e4c910
subscription_id   : f0d51890-8a1a-406d-89a5-5c86986e1def
is_enabled        : True
event_types       : {user.account.created}
description       : Tutorial webhook endpoint
secret            : e7b81824-4b4d-4325-99b5-544dc34ed2e5
metadata          :
label_key         : environment
label_value       : tutorial
labels            : @{environment=tutorial}
target            : @{type=http; method=POST; url=https://play.hook0.com/in/c_80O0yKdQNrJ8v6ENHjzZhLvK8ks/; headers=}
created_at        : 2026-10-08T07:56:07.795845Z
updated_at        : 2026-10-08T07:56:07.795845Z
dedicated_workers : {}
```

### Send Your First Event (step 6 on the guide)

```powershell
$body = @{
  application_id = "$env:APP_ID"
  event_type = "user.account.created"
  payload = '{"user_id": 123, "email": "john.doe@example.com"}'
  payload_content_type = "application/json"
  labels = @{ "environment" = "tutorial" }
  occurred_at = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
} | ConvertTo-Json

$response = Invoke-WebRequest -Uri "$env:HOOK0_API/event" -Headers $headers -Method POST -Body $body

$response.Content | ConvertFrom-Json
```

Output:

```powershell
application_id                       event_id                             received_at
--------------                       --------                             -----------
4698d326-e61f-44f1-bf3d-1518c8e4c910 01a11eb0-ecc3-7916-9014-5f0fc05b1dd5 2026-10-09T03:24:33.859554Z
```

> Note: In Hook0 dashboard, you can view it in the "Inbound Events" tab.

### Verify Webhook Signature (step 8 on the guide)

In Hook0 dashboard, I saw in the Subscriptions tab, the <u>subscription secret</u> is used to **verify webhook signatures**.

It seems I may have to create a simple API to verify the signature.

A small issue:

When I created a .NET Minimal API project and I tried to ran it for the first time, it couldn't compile properly because of the OpenApi issue. To solve this, simply go to the toolbar above:

1. Build > Clean Solution
2. Build > Rebuild Solution

Output:

```
Rebuild started at 9:42 PM...
Waiting for background operations to complete...
1>------ Rebuild All started: Project: VerifyWebhookSignature, Configuration: Debug Any CPU ------
Restored C:\Users\Admin\source\repos\VerifyWebhookSignature\VerifyWebhookSignature\VerifyWebhookSignature.csproj (in 1.78 min).
1>  VerifyWebhookSignature -> C:\Users\Admin\source\repos\VerifyWebhookSignature\VerifyWebhookSignature\bin\Debug\net10.0\VerifyWebhookSignature.dll
========== Rebuild All: 1 succeeded, 0 failed, 0 skipped ==========
========== Rebuild completed at 9:44 PM and took 01:52.538 minutes ==========

```

> Note: Make sure you're connected to the internet since it will pull NuGet packages to resolve the issue.

Below is an example of `x-hook-signature` from the headers:

```
t=1791900316,h=content-type x-custom-header x-event-id x-event-type,v1=f1620915d0970e0babef96540a408c67df3c7b7ee1ffa173332df64c9fcb83b4
```

| Signature Component | Description                                                |
| :------------------ | :--------------------------------------------------------- |
| **`t`**             | Unix timestamp (seconds)                                   |
| **`h`**             | Space-separated list of header names included in signature |
| **`v1`**            | HMAC-SHA256 signature (hex-encoded)                        |


After recreating the sample implementation in .NET, the `parts.v1` and `expected` doesn't match seem to match, so I decided to recreate it using Node.js to be exactly the same as on the example and compare the values to hopefully find out where I went wrong.

<u>Solution</u>

Finally, after so much debugging and confusion, `parts.v1` and `expected` now have matching values. My intuition was correct that my implementation matches with the *Node.js Implementation* on the guide. What I got incorrect was the request.

It should be like this:

```
@VerifyWebhookSignature_HostAddress = https://localhost:7233

###

POST {{VerifyWebhookSignature_HostAddress}}/webhooks/secure
Content-Type: application/json
X-Event-Id: 01a1b351-2e35-7491-98b9-9aa805bb24a6
X-Event-Type: user.account.created
X-Custom-Header: my-value
X-Hook0-Signature: t=1794009805,h=content-type x-custom-header x-event-id x-event-type,v1=7cc566e6d74f8d192731a54ffcdf9d1814d3db4634a47cc403efaab9ba4bf62d

{"user_id": 123, "email": "john.doe@example.com"}

###
```

> **Note**: This the `.http` file on the solution.

The request body should be **STRICTLY** formatted like the above (one-liner) because previously, I placed new lines and formatted it differently that's why it's producing a different value when being computed using the hash.

### WebHook Testing

For testing webhook delivery, I used <https://api-tester.com/webhook-tester/>.

