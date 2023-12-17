// The file contents for the current environment will overwrite these during build.
// The build system defaults to the dev environment which uses `environment.ts`, but if you do
// `ng build --env=prod` then `environment.prod.ts` will be used instead.
// The list of which env maps to which file can be found in `.angular-cli.json`.

export const environment = {
  production: false,
  //apiURL: 'http://180.211.137.74:8088/pvims/api',
  apiURL: 'https://localhost:44380/api',
  //apiURL: 'https://pvims-training-api.doh.gov.ph/api',
  apiURLBase: 'http://180.211.137.74:8088/pvims',
  //apiURLBase: 'http://180.211.137.74:8088/pvims',
  //apiURLBase: 'https://pvims-training-api.doh.gov.ph',
  appVersion: '2.1.0',
  appName: 'PViMS',
  installationDate: '2020-06-24',
  countryISOCode: 'bd',
};
