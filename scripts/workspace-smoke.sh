#!/usr/bin/env bash
#
# Exercises single-company staff against the running stack: GET /api/v1/users/me/workspace,
# company resolution from the staff row, and the X-Company-Id header for a platform administrator.
#
# One throwaway account walks through four states: customer, staff with company_admin at A,
# deactivated staff, and superadmin with no staff row, then calls every moved route as that
# superadmin at B. Companies A and B, their roles, the staff row and every grant the script adds
# (the global super_admin one included) are removed on exit, pass or fail.
#
# Usage:
#   scripts/workspace-smoke.sh                    # registers a throwaway account first
#   scripts/workspace-smoke.sh me@example.com     # uses an existing verified account
#
# Requires: curl, docker compose (psql and redis-cli are reached inside the containers).
# The account must be verified, and must not already be staff anywhere.

set -euo pipefail

KEYCLOAK=${KEYCLOAK_URL:-http://localhost:7030}
API=${API_URL:-http://localhost:7000}
REALM=${REALM:-koureia}
CLIENT=${CLIENT_ID:-koureia-spa}
PASSWORD=${SIGNUP_PASSWORD:-Passw0rd!}

COMPANY_A=aaaa0000-0000-4000-8000-000000000001
COMPANY_B=bbbb0000-0000-4000-8000-000000000002
UNKNOWN_COMPANY=cccc0000-0000-4000-8000-000000000003
SUPER_ADMIN_ROLE=10000000-0000-4000-8000-000000000001
COMPANY_ADMIN_ROLE=10000000-0000-4000-8000-000000000002
LANG_EN=11111122-2222-4111-8111-111111111111
LANG_EL=11111122-2222-4111-8111-111111111112

WORK=$(mktemp -d)
CHECKS=0
USER_ID=

step() { printf '\n\033[1m%s\033[0m\n' "$*"; }
pass() { CHECKS=$((CHECKS + 1)); printf '  \033[32m✓\033[0m %s\n' "$*"; }
die()  { printf '\n\033[31mFAILED: %s\033[0m\n' "$*" >&2; exit 1; }

psql_do() { docker compose exec -T postgres psql -U admin -d aptison_booking_db_dev -q -v ON_ERROR_STOP=1 "$@"; }

teardown() {
    psql_do \
        -c "DELETE FROM user_roles WHERE owner_id IN ('$COMPANY_A','$COMPANY_B');" \
        -c "DELETE FROM user_roles WHERE user_id = '${USER_ID:-00000000-0000-0000-0000-000000000000}' AND role_id = '$SUPER_ADMIN_ROLE';" \
        -c "DELETE FROM staff_members WHERE owner_id IN ('$COMPANY_A','$COMPANY_B');" \
        -c "DELETE FROM role_permissions WHERE role_id IN (SELECT id FROM roles WHERE owner_id IN ('$COMPANY_A','$COMPANY_B'));" \
        -c "DELETE FROM roles WHERE owner_id IN ('$COMPANY_A','$COMPANY_B');" \
        -c "DELETE FROM companies WHERE id IN ('$COMPANY_A','$COMPANY_B');" >/dev/null 2>&1 || true
    [ -n "$USER_ID" ] && evict "$USER_ID" || true
    rm -rf "$WORK"
}
trap teardown EXIT

# A SQL-written grant or staff row cannot call IPermissionCacheInvalidator, and nothing expires —
# so the cached access entry has to be dropped by hand or the change stays invisible.
evict() { docker compose exec -T redis redis-cli DEL "auth:perm:access:$1" >/dev/null; }

# status <path> [header]: prints the HTTP status, body to $WORK/body.json.
status() {
    local extra=()
    [ $# -ge 2 ] && extra=(-H "$2")
    curl -sS -o "$WORK/body.json" -w '%{http_code}' -H "Authorization: Bearer $TOKEN" "${extra[@]}" "$API/api/v1$1"
}
body() { cat "$WORK/body.json"; }

expect() { # expect <status> <path> [header]
    local want=$1; shift
    local got
    got=$(status "$@")
    [ "$got" = "$want" ] || die "GET $1 ${2:-}: expected $want, got $got: $(body)"
}

expect_send() { # expect_send <status> <method> <path> <json|-> [header]
    local want=$1 method=$2 path=$3 payload=$4; shift 4
    local extra=()
    [ $# -ge 1 ] && extra=(-H "$1")
    [ "$payload" != - ] && extra+=(-H "Content-Type: application/json" --data "$payload")
    local got
    got=$(curl -sS -X "$method" -o "$WORK/body.json" -w '%{http_code}' \
        -H "Authorization: Bearer $TOKEN" "${extra[@]}" "$API/api/v1$path")
    [ "$got" = "$want" ] || die "$method $path: expected $want, got $got: $(body)"
}

# --- The account ------------------------------------------------------------------------
if [ $# -ge 1 ]; then
    EMAIL=$1
else
    step "0. Registering a throwaway verified account (scripts/signup-smoke.sh)"
    bash "$(dirname "$0")/signup-smoke.sh" > "$WORK/signup.log" 2>&1 \
        || { cat "$WORK/signup.log"; die "could not create an account to test with"; }
    EMAIL=$(grep -oE 'signup[0-9]+@example\.com' "$WORK/signup.log" | head -1)
    printf '  registered %s\n' "$EMAIL"
fi

printf '\033[1mWorkspace smoke test\033[0m — %s\n' "$EMAIL"

# Direct grant: on koureia-spa for scripts like this one, and scheduled off before production.
TOKEN=$(curl -sS -X POST "$KEYCLOAK/realms/$REALM/protocol/openid-connect/token" \
    -d "client_id=$CLIENT" -d grant_type=password -d "username=$EMAIL" -d "password=$PASSWORD" \
    -d scope=openid | grep -o '"access_token":"[^"]*"' | cut -d'"' -f4 || true)
[ -n "$TOKEN" ] || die "could not obtain a token for $EMAIL — wrong password, or the account is unverified"

USER_ID=$(curl -sS -H "Authorization: Bearer $TOKEN" "$API/api/v1/users/me" \
    | grep -o '"id":"[^"]*"' | head -1 | cut -d'"' -f4)
[ -n "$USER_ID" ] || die "GET /users/me did not answer — is the account verified?"

# --- Fixture: two companies, nothing linking the account to either -----------------------
psql_do <<SQL
INSERT INTO companies (id, slug, email, phone, location, postal_code, country_code, timezone) VALUES
 ('$COMPANY_A','smoke-studio-elpida','a@shop.local','+302100000001',
  ST_SetSRID(ST_MakePoint(23.72,37.98),4326)::geography,'10431','GR','Europe/Athens'),
 ('$COMPANY_B','smoke-kourio-adelfoi','b@shop.local','+302100000002',
  ST_SetSRID(ST_MakePoint(23.73,37.99),4326)::geography,'10432','GR','Europe/Athens');

INSERT INTO company_translations (company_id, language_id, name, street, city) VALUES
 ('$COMPANY_A','$LANG_EN','Studio Elpida','Ermou 10','Athens'),
 ('$COMPANY_A','$LANG_EL','Στούντιο Ελπίδα','Ερμού 10','Αθήνα'),
 ('$COMPANY_B','$LANG_EL','Κουρείο Αδελφοί','Σόλωνος 5','Αθήνα');
SQL
evict "$USER_ID"

# --- 1. A customer ----------------------------------------------------------------------
step "1. A customer"
expect 200 /users/me/workspace
body | grep -q '"company":null' || die "a customer should have no company: $(body)"
body | grep -q '"permissions":\[\]' || die "the global customer role carries no codes: $(body)"
body | grep -q '"isPlatformAdmin":false' || die "a customer is not a platform admin: $(body)"
pass "workspace: no company, no codes, not a platform admin"

expect 403 /roles
body | grep -q 'COMPANY_SCOPE_MISSING' || die "expected COMPANY_SCOPE_MISSING: $(body)"
pass "a company endpoint refuses a customer with 403 COMPANY_SCOPE_MISSING"

# --- 2. Staff with company_admin at A ---------------------------------------------------
step "2. Staff with company_admin at A"
psql_do <<SQL
INSERT INTO staff_members (id, owner_id, user_id, job_title_code) VALUES
 (gen_random_uuid(), '$COMPANY_A', '$USER_ID', 'barber');
INSERT INTO user_roles (id, user_id, role_id, owner_id) VALUES
 (gen_random_uuid(), '$USER_ID', '$COMPANY_ADMIN_ROLE', '$COMPANY_A');
SQL
evict "$USER_ID"

PREFERRED_LANGUAGE=$(psql_do -tAc "SELECT l.code FROM users u JOIN languages l ON l.id = u.preferred_language_id WHERE u.id = '$USER_ID';")
[ "$PREFERRED_LANGUAGE" = "el" ] && NAME_A='Στούντιο Ελπίδα' || NAME_A='Studio Elpida'

expect 200 /users/me/workspace
body | grep -q "\"company\":{\"id\":\"$COMPANY_A\",\"name\":\"$NAME_A\"}" \
    || die "the company should be A, named in the caller's language ($PREFERRED_LANGUAGE): $(body)"
pass "workspace: company A, named in the caller's preferred language"

body | grep -q '"roles:assign"' || die "company_admin's codes should apply at A: $(body)"
pass "permissions are company_admin's at A"

expect 200 /roles
pass "GET /roles answers with no company in the URL"
expect 200 /appointments/odata
pass "GET /appointments/odata answers with no company in the URL"

expect 200 /users/me/workspace "X-Company-Id: $COMPANY_B"
body | grep -q "\"id\":\"$COMPANY_A\"" || die "a staff member's X-Company-Id must be ignored: $(body)"
pass "X-Company-Id from someone who is not a superadmin is ignored"

expect 403 /platform/roles
pass "a company grant of roles:read does not pass GET /platform/roles"

# --- 3. Deactivated -------------------------------------------------------------------
step "3. The staff row deactivated"
psql_do -c "UPDATE staff_members SET is_active = false WHERE owner_id = '$COMPANY_A';"
evict "$USER_ID"

expect 200 /users/me/workspace
body | grep -q '"company":null' || die "a deactivated barber should have no company: $(body)"
pass "workspace: no company once the staff row is inactive"
expect 403 /roles
body | grep -q 'COMPANY_SCOPE_MISSING' || die "expected COMPANY_SCOPE_MISSING: $(body)"
pass "company endpoints refuse again, though the grant at A is still there"

psql_do -c "UPDATE staff_members SET is_active = true WHERE owner_id = '$COMPANY_A';"
evict "$USER_ID"
expect 200 /roles
pass "reactivating restores access with the same roles"

# --- 4. Superadmin with no staff row ----------------------------------------------------
step "4. A superadmin with no staff row"
psql_do <<SQL
DELETE FROM user_roles WHERE owner_id = '$COMPANY_A';
DELETE FROM staff_members WHERE owner_id = '$COMPANY_A';
INSERT INTO user_roles (id, user_id, role_id, owner_id) VALUES
 (gen_random_uuid(), '$USER_ID', '$SUPER_ADMIN_ROLE', NULL);
SQL
evict "$USER_ID"

expect 403 /roles
body | grep -q 'COMPANY_SCOPE_MISSING' || die "a superadmin with no header should get COMPANY_SCOPE_MISSING: $(body)"
pass "without X-Company-Id, a company endpoint answers 403 COMPANY_SCOPE_MISSING"

expect 200 /roles "X-Company-Id: $COMPANY_B"
pass "with X-Company-Id, the superadmin reaches company B"

expect 200 /users/me/workspace "X-Company-Id: $COMPANY_B"
body | grep -q "\"id\":\"$COMPANY_B\"" || die "the workspace should be company B: $(body)"
body | grep -q '"isPlatformAdmin":true' || die "the superadmin should be flagged: $(body)"
body | grep -q '"platform:manage"' && die "a global grant must not count at a company: $(body)"
pass "workspace: company B, isPlatformAdmin true, and the global codes do not count there"

expect 404 /roles "X-Company-Id: $UNKNOWN_COMPANY"
body | grep -q 'COMPANY_NOT_FOUND' || die "expected COMPANY_NOT_FOUND: $(body)"
pass "an unknown company in X-Company-Id is 404 COMPANY_NOT_FOUND"

expect 200 /platform/roles
pass "GET /platform/roles answers the superadmin"
expect 200 /platform/companies/odata
body | grep -q "$COMPANY_B" || die "the shop picker should list company B: $(body)"
pass "GET /platform/companies/odata lists the companies"

expect 200 "/platform/users/$USER_ID"
pass "GET /platform/users/{id} answers the superadmin"

# --- 5. The rest of the moved routes, as the superadmin at B ----------------------------
step "5. Every moved company route answers at its new path"
AT_B="X-Company-Id: $COMPANY_B"

for path in /appointments/calendar-options /appointments/customers/odata /roles/odata \
            /roles/grantable-permissions "/roles/assignments?userId=$USER_ID" "/roles/$COMPANY_ADMIN_ROLE"; do
    expect 200 "$path" "$AT_B"
done
pass "calendar-options, customers/odata, roles/odata, grantable-permissions, assignments, roles/{id}"

expect_send 200 POST /roles '{"name":"smoke-role","permissions":["appointments:read_all"]}' "$AT_B"
ROLE_ID=$(body | grep -o '"id":"[^"]*"' | head -1 | cut -d'"' -f4)
expect_send 200 PUT "/roles/$ROLE_ID" '{"name":"smoke-role-renamed","permissions":["appointments:read_all"]}' "$AT_B"
expect_send 204 DELETE "/roles/$ROLE_ID" - "$AT_B"
pass "POST /roles, then PUT and DELETE /roles/{id}, at company B"

# The superadmin is not on B's staff, so the handler refuses: the route and the scope both worked.
expect_send 422 POST /roles/assignments "{\"userId\":\"$USER_ID\",\"roleId\":\"$COMPANY_ADMIN_ROLE\"}" "$AT_B"
body | grep -q 'ROLE_ASSIGNMENT_NOT_STAFF_MEMBER' || die "expected ROLE_ASSIGNMENT_NOT_STAFF_MEMBER: $(body)"
expect_send 404 DELETE "/roles/assignments/$USER_ID/$COMPANY_ADMIN_ROLE" - "$AT_B"
body | grep -q 'ROLE_ASSIGNMENT_NOT_FOUND' || die "expected ROLE_ASSIGNMENT_NOT_FOUND: $(body)"
pass "POST /roles/assignments and DELETE /roles/assignments/{userId}/{roleId} reach their handlers"

printf '\n\033[32m%s checks passed.\033[0m Fixture removed.\n' "$CHECKS"
