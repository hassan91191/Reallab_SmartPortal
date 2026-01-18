const { google } = require('googleapis');

function parseServiceAccountJson() {
  const raw = process.env.GOOGLE_SERVICE_ACCOUNT_JSON;
  if (!raw) throw new Error('Missing env GOOGLE_SERVICE_ACCOUNT_JSON');

  // Allow: raw JSON, base64 JSON, or escaped JSON string
  let txt = raw.trim();
  if (txt.startsWith('base64:')) {
    txt = Buffer.from(txt.slice('base64:'.length), 'base64').toString('utf8');
  }

  try {
    return JSON.parse(txt);
  } catch (e) {
    // Try un-escape newlines\n etc
    try {
      const fixed = txt.replace(/\\n/g, '
');
      return JSON.parse(fixed);
    } catch (_) {
      throw new Error('Invalid GOOGLE_SERVICE_ACCOUNT_JSON (not valid JSON)');
    }
  }
}

let cached = null;
function getClients() {
  if (cached) return cached;
  const sa = parseServiceAccountJson();
  const scopes = [
    'https://www.googleapis.com/auth/drive.readonly',
    'https://www.googleapis.com/auth/spreadsheets',
  ];

  const auth = new google.auth.JWT({
    email: sa.client_email,
    key: sa.private_key,
    scopes,
  });

  cached = {
    auth,
    drive: google.drive({ version: 'v3', auth }),
    sheets: google.sheets({ version: 'v4', auth }),
    serviceAccountEmail: sa.client_email,
  };
  return cached;
}

module.exports = { getClients };
