#!/usr/bin/env bash
#
# Drives the real signup flow end to end without a browser or an Angular app:
# register -> exchange the code for a token -> be refused by the gate -> ask the API for a
# verification code -> read it out of Mailpit -> verify -> refresh -> call the API.
#
# Every step goes through the same endpoints a browser would use, including PKCE and
# Keycloak's HTML forms, so a realm change that breaks signup breaks this too. The only
# thing it fakes is the human: form fields are posted directly instead of typed, and the
# code email is read out of Mailpit's API instead of an inbox.
#
# With verifyEmail false, registration completes the authorization-code flow: no second login,
# and the API owns the code.
#
# Usage:
#   scripts/signup-smoke.sh                     # generates a unique throwaway address
#   scripts/signup-smoke.sh me@example.com      # uses the address you give it
#
# Requires: curl, openssl. Both ship with Git Bash.

set -euo pipefail

KEYCLOAK=${KEYCLOAK_URL:-http://localhost:7030}
MAILPIT=${MAILPIT_URL:-http://localhost:7050}
API=${API_URL:-http://localhost:7000}
REALM=${REALM:-koureia}
CLIENT=${CLIENT_ID:-koureia-spa}
CLIENT_SECRET=${CLIENT_SECRET:-}
# The URI must match a registered redirect URI for CLIENT. The default value matches `koureia-spa`.
# Port 6100 is the Koureia.Web development port. This test does not need a server on that port.
REDIRECT=${REDIRECT_URI:-http://localhost:6100/}
PASSWORD=${SIGNUP_PASSWORD:-Passw0rd!}

CLIENT_AUTH_ARGS=()
if [ -n "$CLIENT_SECRET" ]; then
    CLIENT_AUTH_ARGS=(-d "client_secret=$CLIENT_SECRET")
fi

EMAIL=${1:-signup$(date +%s)@example.com}
FIRST_NAME=${FIRST_NAME:-Sign}
LAST_NAME=${LAST_NAME:-Up}

JAR=$(mktemp)
WORK=$(mktemp -d)
trap 'rm -rf "$JAR" "$WORK"' EXIT

step() { printf '\n\033[1m%s\033[0m\n' "$*"; }
info() { printf '  %s\n' "$*"; }
die()  { printf '\n\033[31mFAILED: %s\033[0m\n' "$*" >&2; exit 1; }

# Keycloak emits form actions with HTML-escaped ampersands; they must be unescaped before
# the URL is used, or every query parameter after the first is lost.
first_form_action() { grep -oE 'action="[^"]*"' "$1" | head -1 | sed 's/action="//; s/"$//; s/&amp;/\&/g'; }

# The Keycloakify theme renders its forms in the browser from an embedded kcContext object, so
# there is no <form> to scrape: the page is named by pageId and the action is a URL in kcContext.
# Both readers also accept the stock theme's HTML, so the script survives a theme switch.
kc_field() { grep -oE "\"$2\": \"[^\"]*\"" "$1" | head -1 | cut -d'"' -f4 | sed 's#\\/#/#g; s/\\u0026/\&/g'; }
is_register_page() { grep -q 'name="password-confirm"' "$1" || [ "$(kc_field "$1" pageId)" = "register" ]; }
registration_action() { local action; action=$(kc_field "$1" registrationAction); echo "${action:-$(first_form_action "$1")}"; }
kc_message() { kc_field "$1" summary; }

urlencode() { printf '%s' "$1" | od -An -tx1 | tr -d '\n ' | sed 's/\(..\)/%\1/g'; }

json_string() { grep -oE "\"$2\":\"[^\"]*\"" "$1" | head -1 | cut -d'"' -f4; }

# --- PKCE -------------------------------------------------------------------------------
# The client sets pkce.code.challenge.method=S256, so an authorization request without a
# challenge is rejected outright. Not optional, and not only for browsers.
new_verifier() { openssl rand -hex 32; }
challenge_for() { printf '%s' "$1" | openssl dgst -sha256 -binary | openssl base64 | tr '+/' '-_' | tr -d '='; }

authorize_url() { # <endpoint> <challenge>
    printf '%s/realms/%s/protocol/openid-connect/%s?client_id=%s&response_type=code&scope=openid&redirect_uri=%s&code_challenge=%s&code_challenge_method=S256' \
        "$KEYCLOAK" "$REALM" "$1" "$CLIENT" "$(urlencode "$REDIRECT")" "$2"
}

# Follows redirects by hand: -L would chase the last hop to $REDIRECT, which nothing serves,
# and fail on the connection instead of yielding the code.
code_from_redirects() { # <cookie jar> <first location>
    local jar=$1 location=$2 hops=0 headers
    while [ -n "$location" ] && [ "$hops" -lt 5 ]; do
        case "$location" in
            *code=*) printf '%s' "$location" | grep -oE 'code=[^&[:space:]]*' | cut -d= -f2; return 0 ;;
        esac
        headers=$(curl -sS -b "$jar" -c "$jar" -o /dev/null -D - "$location")
        location=$(printf '%s' "$headers" | grep -i '^location:' | tail -1 | sed 's/^[Ll]ocation: *//; s/\r$//')
        hops=$((hops + 1))
    done
    return 1
}

printf '\033[1mSignup smoke test\033[0m — %s\n' "$EMAIL"

# --- 1. Registration form ---------------------------------------------------------------
step "1. Opening the registration form"
VERIFIER=$(new_verifier)
curl -sS -c "$JAR" -L "$(authorize_url registrations "$(challenge_for "$VERIFIER")")" -o "$WORK/register.html" \
    || die "could not reach Keycloak at $KEYCLOAK"
is_register_page "$WORK/register.html" \
    || die "that is not the registration form — is registrationAllowed still true?"
info "form served (email, firstName, lastName, password)"

# --- 2. Submit it -----------------------------------------------------------------------
# No VERIFY_EMAIL required action stands any more, so this POST ends the code flow:
# redirect_uri?code=… and the person is signed in.
step "2. Registering $EMAIL"
REG_HEADERS=$(curl -sS -b "$JAR" -c "$JAR" -X POST "$(registration_action "$WORK/register.html")" \
    --data-urlencode "email=$EMAIL" \
    --data-urlencode "firstName=$FIRST_NAME" \
    --data-urlencode "lastName=$LAST_NAME" \
    --data-urlencode "password=$PASSWORD" \
    --data-urlencode "password-confirm=$PASSWORD" \
    -o "$WORK/registered.html" -D -)

# Keycloak redisplays the form when it rejects the submission, so landing on the register page
# again is the reliable failure signal.
if is_register_page "$WORK/registered.html"; then
    kc_message "$WORK/registered.html"
    sed 's/<[^>]*>//g' "$WORK/registered.html" \
        | grep -iE 'already|invalid|must be|not a valid|specify' | head -3 || true
    die "Keycloak rejected the registration (address already taken?)"
fi

LOCATION=$(printf '%s' "$REG_HEADERS" | grep -i '^location:' | tail -1 | sed 's/^[Ll]ocation: *//; s/\r$//')
CODE=$(code_from_redirects "$JAR" "$LOCATION") \
    || die "registration did not end in an authorization code — is the realm's verifyEmail back to true, or is another required action configured?"
info "account created and signed in — registration completed the code flow, no second login"

# --- 3. Exchange the code ---------------------------------------------------------------
step "3. Exchanging the code for a token set"
curl -sS -X POST "$KEYCLOAK/realms/$REALM/protocol/openid-connect/token" \
    -d "client_id=$CLIENT" -d grant_type=authorization_code -d "code=$CODE" \
    -d "redirect_uri=$REDIRECT" -d "code_verifier=$VERIFIER" \
    "${CLIENT_AUTH_ARGS[@]}" -o "$WORK/token.json"

TOKEN=$(json_string "$WORK/token.json" access_token)
REFRESH=$(json_string "$WORK/token.json" refresh_token)
[ -n "$TOKEN" ] && [ -n "$REFRESH" ] || { cat "$WORK/token.json"; die "the code could not be exchanged for a token"; }
info "access and refresh tokens obtained — email_verified is false on this pair"

# --- 4. The gate ------------------------------------------------------------------------
# Asserted, not assumed: if this call succeeds, the realm flip removed a security property
# without its replacement in place.
step "4. Confirming the gate refuses this token"
STATUS=$(curl -sS -o "$WORK/gated.json" -w '%{http_code}' -H "Authorization: Bearer $TOKEN" "$API/api/v1/users/me")
[ "$STATUS" = "403" ] \
    || die "GET /users/me returned $STATUS before verification — the email_verified gate is not holding"
grep -q 'AUTH_EMAIL_NOT_VERIFIED' "$WORK/gated.json" \
    || { cat "$WORK/gated.json"; die "the 403 did not carry AUTH_EMAIL_NOT_VERIFIED — a bare framework 403 is indistinguishable from a permission denial"; }
info "403 AUTH_EMAIL_NOT_VERIFIED, with a problem body a client can act on"

# --- 5. Ask for a code ------------------------------------------------------------------
# No body: subject and address come off the token, so it cannot be aimed at a stranger.
step "5. Asking the API for a verification code"
STATUS=$(curl -sS -o "$WORK/sent.json" -w '%{http_code}' -X POST \
    -H "Authorization: Bearer $TOKEN" \
    "$API/api/v1/auth/email-verification/code")
[ "$STATUS" = "200" ] || { cat "$WORK/sent.json"; die "POST /auth/email-verification/code returned $STATUS"; }
info "code issued: $(sed 's/[{}"]//g' "$WORK/sent.json")"

# --- 6. Read it out of Mailpit ----------------------------------------------------------
# The subject line leads with the digits, which is what makes it cheap to extract.
step "6. Waiting for the code email in Mailpit"
VERIFICATION_CODE=""
for _ in $(seq 1 20); do
    SUBJECT=$(curl -sS "$MAILPIT/api/v1/search?query=to%3A$EMAIL" \
        | grep -oE '"Subject":"[^"]*"' | head -1 | cut -d'"' -f4 || true)
    VERIFICATION_CODE=$(printf '%s' "$SUBJECT" | grep -oE '^[0-9]+' || true)
    [ -n "$VERIFICATION_CODE" ] && break
    sleep 1
done
[ -n "$VERIFICATION_CODE" ] \
    || die "no code email arrived — check the API's Smtp__ settings, the mailpit container, and the outbox dispatcher"
info "code received: $VERIFICATION_CODE"

# --- 7. Verify --------------------------------------------------------------------------
step "7. Submitting the code"
STATUS=$(curl -sS -o "$WORK/verified.json" -w '%{http_code}' -X POST \
    -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
    -d "{\"code\":\"$VERIFICATION_CODE\"}" \
    "$API/api/v1/auth/email-verification/code/verify")
[ "$STATUS" = "200" ] || { cat "$WORK/verified.json"; die "POST /auth/email-verification/code/verify returned $STATUS"; }
grep -q '"outcome":"Verified"' "$WORK/verified.json" \
    || { cat "$WORK/verified.json"; die "the code was not accepted"; }
info "address verified; tokenRefreshRequired says the current token is still shut out"

# --- 8. Refresh -------------------------------------------------------------------------
# Flipping emailVerified cannot alter an already-signed token. Skip this and a verified person
# is refused everywhere.
step "8. Refreshing the token so the new claim is in it"
curl -sS -X POST "$KEYCLOAK/realms/$REALM/protocol/openid-connect/token" \
    -d "client_id=$CLIENT" -d grant_type=refresh_token -d "refresh_token=$REFRESH" \
    "${CLIENT_AUTH_ARGS[@]}" -o "$WORK/refreshed.json"
TOKEN=$(json_string "$WORK/refreshed.json" access_token)
[ -n "$TOKEN" ] || { cat "$WORK/refreshed.json"; die "the refresh token could not be exchanged"; }
info "new access token carries email_verified: true"

# --- 9. First real API call -------------------------------------------------------------
step "9. Calling the API — this is the request that creates the user row"
STATUS=$(curl -sS -o "$WORK/me.json" -w '%{http_code}' -H "Authorization: Bearer $TOKEN" "$API/api/v1/users/me")
cat "$WORK/me.json"; echo
[ "$STATUS" = "200" ] || die "GET /users/me returned $STATUS"

# The BFF uses this logout flow. Public clients leave this optional test off.
if [ "${TEST_BACKCHANNEL_LOGOUT:-false}" = "true" ]; then
    [ -n "$CLIENT_SECRET" ] || die "back-channel logout needs CLIENT_SECRET"

    step "10. Ending the Keycloak session through the back channel"
    STATUS=$(curl -sS -o "$WORK/logout.json" -w '%{http_code}' -X POST \
        "$KEYCLOAK/realms/$REALM/protocol/openid-connect/logout" \
        -d "client_id=$CLIENT" -d "client_secret=$CLIENT_SECRET" -d "refresh_token=$REFRESH")
    [ "$STATUS" = "204" ] || { cat "$WORK/logout.json"; die "back-channel logout returned $STATUS"; }

    STATUS=$(curl -sS -o "$WORK/after-logout.json" -w '%{http_code}' -X POST \
        "$KEYCLOAK/realms/$REALM/protocol/openid-connect/token" \
        -d "client_id=$CLIENT" -d grant_type=refresh_token -d "refresh_token=$REFRESH" \
        -d "client_secret=$CLIENT_SECRET")
    [ "$STATUS" = "400" ] || die "the refresh token remained valid after logout"
    info "the logout endpoint ended the SSO session and invalidated the refresh token"
fi

printf '\n\033[32mSignup flow completed.\033[0m %s now has a users row and the customer role.\n' "$EMAIL"
printf 'Token for further calls:\n\n  export TOKEN=%s\n\n' "$TOKEN"
printf 'Confirm the grant with:\n\n  docker compose exec postgres psql -U admin -d aptison_booking_db_dev \\\n'
printf "    -c \"SELECT u.email, r.name FROM users u JOIN user_roles ur ON ur.user_id=u.id JOIN roles r ON r.id=ur.role_id WHERE u.email='%s';\"\n\n" "$EMAIL"
