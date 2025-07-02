
add loginnname in tblusers

rm is used for the access level, admin, main and local, all lowercase

Copy all the usernames to login names
Login name will be used to log in from now onwards, 
new registers will use bcrypt.
UPDATE tblusers
SET loginname = COALESCE(username, loginname);

IMPORTANT
Check for /
SELECT id, qarid
FROM tblincident
WHERE qarid LIKE '%/%';

update all / with -
UPDATE tblincident
SET qarid = REPLACE(qarid, '/', '-')
WHERE qarid LIKE '%/%';


CREATE THIS 
CREATE TABLE tblriskmanagement (
    id SERIAL PRIMARY KEY,
    qarid VARCHAR(50) UNIQUE NOT NULL,
    risktitle TEXT NOT NULL,
    rootcause TEXT,
    nearrootcause TEXT,
    risktype VARCHAR(100),
    risklevel VARCHAR(50),
    preventativeaction TEXT,
    responsibleperson VARCHAR(100),
    targetdate DATE,
    status VARCHAR(50) DEFAULT 'Open',
    dateidentified TIMESTAMP NOT NULL,
    inserthospitalid VARCHAR(50) NOT NULL,
    active CHAR(1) DEFAULT 'Y',
    datecaptured DATE,
    capturedby VARCHAR(100)
);


CREATE TABLE tblincidentlog (
    id SERIAL PRIMARY KEY,
    qarid VARCHAR,
    hospitalid VARCHAR,
    actiontype VARCHAR,          -- e.g., 'Deleted', 'PutOnHold', 'AutoExpireWarning'
    actionby VARCHAR,            -- user or staff name
    actiondate TIMESTAMP,        -- when the action happened
    notes TEXT                   -- optional notes like countdown days left
);


//update user access
UPDATE tblusers
SET rm = 'local'
WHERE rm IS NULL 
   OR rm = '' 
   OR rm = 'message';