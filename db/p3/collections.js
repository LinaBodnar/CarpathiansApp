db = db.getSiblingDB('ReviewContext');

function ensureCollection(name, jsonSchema) {
    const validator = { $jsonSchema: jsonSchema };
    if (db.getCollectionNames().includes(name)) {
        db.runCommand({ collMod: name, validator: validator, validationLevel: 'strict', validationAction: 'error' });
        print('updated validator: ' + name);
    } else {
        db.createCollection(name, { validator: validator });
        print('created collection: ' + name);
    }
}

const userRef = {
    bsonType: 'object',
    required: ['userId', 'displayName'],
    properties: {
        userId:      { bsonType: 'string', description: 'Customers.Id з БД Бронювання (логічне посилання без FK)' },
        displayName: { bsonType: 'string', description: 'знімок Customers.FullName на момент створення' }
    }
};

ensureCollection('reviews', {
    bsonType: 'object',
    required: ['routeId', 'routeName', 'author', 'rating', 'text', 'createdAt'],
    properties: {
        routeId:   { bsonType: ['int', 'long'], minimum: 1 },
        routeName: { bsonType: 'string' },
        author:    userRef,
        rating:    { bsonType: 'int', minimum: 1, maximum: 5 },
        text:      { bsonType: 'string', minLength: 1, maxLength: 5000 },
        photos:    { bsonType: 'array', items: { bsonType: 'string' } },
        comments:  {
            bsonType: 'array',
            items: {
                bsonType: 'object',
                required: ['author', 'text', 'createdAt'],
                properties: {
                    author:    userRef,
                    text:      { bsonType: 'string', minLength: 1 },
                    createdAt: { bsonType: 'date' }
                }
            }
        },
        discussionId:          { bsonType: 'objectId' },
        isVerifiedParticipant: { bsonType: 'bool' },
        gpsTrack: {
            bsonType: 'array',
            items: {
                bsonType: 'object',
                required: ['lat', 'lng'],
                properties: {
                    lat: { bsonType: 'number', minimum: -90,  maximum: 90 },
                    lng: { bsonType: 'number', minimum: -180, maximum: 180 },
                    ele: { bsonType: 'number' }
                }
            }
        },
        weather: { bsonType: 'object' },
        tags:    { bsonType: 'array', items: { bsonType: 'string' } },
        createdAt: { bsonType: 'date' }
    }
});

ensureCollection('discussions', {
    bsonType: 'object',
    required: ['routeId', 'routeName', 'title', 'createdBy', 'createdAt', 'messageCount', 'lastMessages', 'lastActivityAt'],
    properties: {
        routeId:        { bsonType: ['int', 'long'], minimum: 1 },
        routeName:      { bsonType: 'string' },
        title:          { bsonType: 'string', minLength: 1, maxLength: 200 },
        createdBy:      userRef,
        createdAt:      { bsonType: 'date' },
        lastActivityAt: { bsonType: 'date' },
        messageCount:   { bsonType: 'int', minimum: 0 },
        lastMessages: {
            bsonType: 'array',
            maxItems: 3,
            description: 'копії кількох останніх повідомлень для швидкого читання; повні дані в discussionMessages',
            items: {
                bsonType: 'object',
                required: ['author', 'text', 'createdAt'],
                properties: {
                    author:    userRef,
                    text:      { bsonType: 'string' },
                    createdAt: { bsonType: 'date' }
                }
            }
        }
    }
});

ensureCollection('discussionMessages', {
    bsonType: 'object',
    required: ['discussionId', 'author', 'text', 'createdAt'],
    properties: {
        discussionId: { bsonType: 'objectId' },
        author:       userRef,
        text:         { bsonType: 'string', minLength: 1, maxLength: 5000 },
        replyTo:      { bsonType: 'objectId', description: "необов'язкове: id повідомлення, на яке відповідь" },
        attachments:  { bsonType: 'array', items: { bsonType: 'string' } },
        createdAt:    { bsonType: 'date' }
    }
});

ensureCollection('ratings', {
    bsonType: 'object',
    required: ['routeId', 'avgRating', 'count'],
    properties: {
        routeId:      { bsonType: ['int', 'long'], minimum: 1 },
        avgRating:    { bsonType: 'number', minimum: 1, maximum: 5 },
        count:        { bsonType: 'number', minimum: 1 },
        distribution: { bsonType: 'object' },
        updatedAt:    { bsonType: 'date' }
    }
});

db.reviews.createIndex({ routeId: 1, createdAt: -1 }, { name: 'ix_reviews_route_created' });
db.reviews.createIndex({ routeId: 1, 'author.userId': 1 }, { name: 'ux_reviews_route_author', unique: true });
db.reviews.createIndex({ 'author.userId': 1 }, { name: 'ix_reviews_author' });
db.reviews.createIndex({ discussionId: 1 }, { name: 'ix_reviews_discussion', partialFilterExpression: { discussionId: { $exists: true } } });
db.reviews.createIndex({ text: 'text' }, { name: 'tx_reviews_text', default_language: 'none' });

db.discussions.createIndex({ routeId: 1, lastActivityAt: -1 }, { name: 'ix_discussions_route_activity' });

db.discussionMessages.createIndex({ discussionId: 1, createdAt: 1 }, { name: 'ix_messages_discussion_created' });

db.ratings.createIndex({ routeId: 1 }, { name: 'ux_ratings_route', unique: true });

print('ReviewContext initialised');
